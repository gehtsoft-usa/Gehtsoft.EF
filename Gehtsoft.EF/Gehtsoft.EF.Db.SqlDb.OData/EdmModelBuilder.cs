using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Entities;
using System.Data;
using System.ComponentModel.Design;
using System.Reflection;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OData.UriParser;

namespace Gehtsoft.EF.Db.SqlDb.OData
{
    public class EdmModelBuilder
    {
        private readonly Dictionary<string, Tuple<Type, int>> mTypeNameToEntity = new Dictionary<string, Tuple<Type, int>>();
        private readonly Dictionary<Type, List<Tuple<string, string, Type>>> mTypeToFields = new Dictionary<Type, List<Tuple<string, string, Type>>>();
        private readonly List<Tuple<Type, IPropertyAccessor, EdmEntityType, string, bool>> mUnresolvedEntities = new List<Tuple<Type, IPropertyAccessor, EdmEntityType, string, bool>>();

        public IEdmModel Model { get; private set; }

        public Type EntityTypeByName(string odataEntityName) => mTypeNameToEntity[odataEntityName].Item1;
        public int EntityPagingLimitByName(string odataEntityName) => mTypeNameToEntity[odataEntityName].Item2;
        public void SetEntityPagingLimitByName(string odataEntityName, int pagingLimit) => mTypeNameToEntity[odataEntityName] = new Tuple<Type, int>(EntityTypeByName(odataEntityName), pagingLimit);

        public Type TypeByName(Type entityType, string name) => mTypeToFields[entityType].SingleOrDefault(t => t.Item1 == name)?.Item3;
        public string FieldByName(Type entityType, string name) => mTypeToFields[entityType].SingleOrDefault(t => t.Item1 == name)?.Item2;
        public string NameByField(Type entityType, string fieldName) => mTypeToFields[entityType].SingleOrDefault(t => t.Item2 == fieldName)?.Item1;

        public void Build(EntityFinder.EntityTypeInfo[] entities, string ns = "NS")
        {
            var model = new EdmModel();
            EdmEntityContainer container = new EdmEntityContainer(ns, "EntityContainer");
            model.AddElement(container);
            Dictionary<Type, EdmEntityType> dict = new Dictionary<Type, EdmEntityType>();

            Dictionary<EdmEntityType, IEdmStructuralProperty> keys = new Dictionary<EdmEntityType, IEdmStructuralProperty>();
            foreach (var entity in entities)
            {
                int pagingLimit = -1;
                PagingLimitAttribute pagingLimitAttr = entity.EntityType.GetTypeInfo().GetCustomAttribute<PagingLimitAttribute>();
                if (pagingLimitAttr != null)
                {
                    pagingLimit = pagingLimitAttr.PagingLimit;
                }

                string name = entity.EntityType.Name + "_Type";
                EdmEntityType edmEntity = model.AddEntityType(ns, name);
                mTypeNameToEntity[name] = new Tuple<Type, int>(entity.EntityType, pagingLimit);
                var descriptor = AllEntities.Inst[entity.EntityType];
                dict[entity.EntityType] = edmEntity;

                mTypeToFields.Add(entity.EntityType, new List<Tuple<string, string, Type>>());

                foreach (PropertyInfo propertyInfo in entity.EntityType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    PropertyAccessor propertyAccessor = new PropertyAccessor(propertyInfo);
                    EntityPropertyAttribute propertyAttribute = propertyAccessor.GetCustomAttribute<EntityPropertyAttribute>();
                    if (propertyAttribute != null)
                    {
                        string fieldName = propertyAttribute.Field ?? propertyAccessor.Name.ToLower();

                        mTypeToFields[entity.EntityType].Add(Tuple.Create(propertyAccessor.Name, fieldName, propertyAttribute.ForeignKey ? null : propertyInfo.PropertyType));
                    }
                }

                foreach (var property in descriptor.TableDescriptor)
                {
                    if (property.PrimaryKey)
                    {
                        IEdmStructuralProperty entityProperty = edmEntity.AddStructuralProperty(property.PropertyAccessor.Name, SqlTypeToEdmType(property.DbType));
                        edmEntity.AddKeys(entityProperty);
                        keys[edmEntity] = entityProperty;
                    }
                    else if (property.ForeignKey)
                    {
                        string[] arr = property.Name.Split(new char[] { '.' });
                        string propertyName = arr[arr.Length - 1];
                        if (propertyName == property.PropertyAccessor.Name)
                        {
                            propertyName += "ID";
                        }
                        edmEntity.AddStructuralProperty(propertyName, SqlTypeToEdmType(property.DbType));
                        if (dict.ContainsKey(property.PropertyAccessor.PropertyType))
                        {
                            ProcessForeignKey(keys, dict, property.PropertyAccessor.PropertyType, property.PropertyAccessor, edmEntity, entity.EntityType.Name, property.Nullable);
                        }
                        else
                        {
                            mUnresolvedEntities.Add(new Tuple<Type, IPropertyAccessor, EdmEntityType, string, bool>
                                (property.PropertyAccessor.PropertyType, property.PropertyAccessor, edmEntity, entity.EntityType.Name, property.Nullable));
                        }
                    }
                    else
                        edmEntity.AddStructuralProperty(property.PropertyAccessor.Name, SqlTypeToEdmType(property.DbType));
                }

                _ = container.AddEntitySet(entity.EntityType.Name, edmEntity);
            }

            foreach (Tuple<Type, IPropertyAccessor, EdmEntityType, string, bool> unresolved in mUnresolvedEntities)
            {
                ProcessForeignKey(keys, dict, unresolved.Item1, unresolved.Item2, unresolved.Item3, unresolved.Item4, unresolved.Item5);
            }
            Model = model;

            // add custom functions
            try
            {
                CustomUriFunctions.AddCustomUriFunction("trimleft", new FunctionSignatureWithReturnType(
                    EdmCoreModel.Instance.GetString(true), EdmCoreModel.Instance.GetString(true)));
            }
            catch { }
        }

        private void ProcessForeignKey(Dictionary<EdmEntityType, IEdmStructuralProperty> keys, Dictionary<Type, EdmEntityType> dict, Type item1, IPropertyAccessor item2, EdmEntityType item3, string item4, bool item5)
        {
            var referenceEntity = dict[item1];
            IEdmStructuralProperty entityProperty = item3.AddStructuralProperty($"{item2.Name}Ref", new EdmEntityTypeReference(referenceEntity, item5));

            item3.AddUnidirectionalNavigation(new EdmNavigationPropertyInfo()
            {
                Name = $"{item2.Name}",
                Target = referenceEntity,
                PrincipalProperties = new List<IEdmStructuralProperty>()
                            { entityProperty },
                DependentProperties = new List<IEdmStructuralProperty>()
                            { keys[referenceEntity] },
                TargetMultiplicity = EdmMultiplicity.One,
                ContainsTarget = true,
            });

            referenceEntity.AddUnidirectionalNavigation(new EdmNavigationPropertyInfo()
            {
                Name = $"{item4}",
                Target = item3,
                PrincipalProperties = new List<IEdmStructuralProperty>()
                            { keys[referenceEntity] },
                DependentProperties = new List<IEdmStructuralProperty>()
                            { entityProperty },
                TargetMultiplicity = EdmMultiplicity.Many,
                ContainsTarget = false,
            });
        }

        private static EdmPrimitiveTypeKind SqlTypeToEdmType(DbType dbType)
        {
            switch (dbType)
            {
                case System.Data.DbType.String:
                case System.Data.DbType.AnsiString:
                    return EdmPrimitiveTypeKind.String;

                case System.Data.DbType.Int32:
                    return EdmPrimitiveTypeKind.Int32;

                case System.Data.DbType.Int64:
                    return EdmPrimitiveTypeKind.Int64;

                case System.Data.DbType.Boolean:
                    return EdmPrimitiveTypeKind.Boolean;

                case System.Data.DbType.Date:
                case System.Data.DbType.DateTime:
                    return EdmPrimitiveTypeKind.Date;

                case System.Data.DbType.DateTime2:
                    return EdmPrimitiveTypeKind.Date;

                case System.Data.DbType.Single:
                    return EdmPrimitiveTypeKind.Single;

                case System.Data.DbType.Double:
                    return EdmPrimitiveTypeKind.Double;

                case System.Data.DbType.VarNumeric:
                    return EdmPrimitiveTypeKind.Double;

                case System.Data.DbType.Guid:
                    return EdmPrimitiveTypeKind.Guid;

                case System.Data.DbType.Binary:
                    return EdmPrimitiveTypeKind.Binary;

                default:
                    return EdmPrimitiveTypeKind.None;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class PagingLimitAttribute : Attribute
    {
        public int PagingLimit { get; }

        public PagingLimitAttribute(int pagingLimit) : base()
        {
            PagingLimit = pagingLimit;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Mapper
{
    public class EntityMapInitializer : IMapInitializer
    {
        public void SourceToModel(IMap map)
        {
            Type entityType = map.Source;
            Type modelType = map.Destination;
            TypeInfo modelTypeInfo = modelType.GetTypeInfo();
            TypeInfo entityTypeInfo = entityType.GetTypeInfo();

            EntityDescriptor entityDescriptor = AllEntities.Inst[entityType];
            MapEntityAttribute mapEntityAttribute = modelTypeInfo.GetCustomAttribute<MapEntityAttribute>();
            if (mapEntityAttribute == null || mapEntityAttribute.EntityType != entityType)
                throw new ArgumentException("Model type does not have a mapping attribute");

            foreach (PropertyInfo propertyInfo in modelTypeInfo.GetProperties())
            {
                if (propertyInfo.GetCustomAttribute<DoNotAutoMapAttribute>() != null)
                    continue;

                MapPropertyAttribute attribute = propertyInfo.GetCustomAttribute<MapPropertyAttribute>();

                if (attribute?.IgnoreToModel != false)
                    continue;

                string name = attribute.Name ?? propertyInfo.Name;

                IMappingSource source;

                TableDescriptor.ColumnInfo columnInfo;
                try
                {
                    columnInfo = entityDescriptor[name];
                }
                catch (Exception)
                {
                    columnInfo = null;
                }

                if (columnInfo != null)
                {
                    if (columnInfo.PropertyAccessor.GetCustomAttribute<DoNotAutoMapAttribute>() != null)
                        continue;

                    //detect whether the column is a reference
                    if (columnInfo.ForeignKey)
                    {
                        if (propertyInfo.PropertyType.GetTypeInfo().GetCustomAttribute<MapEntityAttribute>() != null)
                            source = new EntityPropertyAccessor(columnInfo); //map the whole reference object
                        else
                            source = new EntityPrimaryKeySource(columnInfo);
                    }
                    else
                        source = new EntityPropertyAccessor(columnInfo);
                }
                else
                {
                    PropertyInfo otherPropertyInfo = entityTypeInfo.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                    if (otherPropertyInfo == null)
                        throw new InvalidOperationException($"Property {name} is not found nor as column of the entity nor as a class property");
                    source = new ClassPropertyAccessor(otherPropertyInfo);
                }

                IMappingTarget target = new ClassPropertyAccessor(propertyInfo);

                IPropertyMapping mapping = map.For(target);
                mapping.Source = source;
                mapping.MapFlag = attribute.MapFlags;
            }
        }

        public void ModelToSource(IMap map)
        {
            Type entityType = map.Destination;
            Type modelType = map.Source;
            TypeInfo modelTypeInfo = modelType.GetTypeInfo();
            TypeInfo entityTypeInfo = entityType.GetTypeInfo();

            EntityDescriptor entityDescriptor = AllEntities.Inst[entityType];
            MapEntityAttribute mapEntityAttribute = modelTypeInfo.GetCustomAttribute<MapEntityAttribute>();
            if (mapEntityAttribute == null || mapEntityAttribute.EntityType != entityType)
                throw new ArgumentException("Model type does not have a mapping attribute");

            foreach (PropertyInfo propertyInfo in modelTypeInfo.GetProperties())
            {
                MapPropertyAttribute attribute = propertyInfo.GetCustomAttribute<MapPropertyAttribute>();
                if (attribute?.IgnoreFromModel != false)
                    continue;

                if (propertyInfo.GetCustomAttribute<DoNotAutoMapAttribute>() != null)
                    continue;

                string name = attribute.Name ?? propertyInfo.Name;

                IMappingSource source = null;
                IMappingTarget target;

                TableDescriptor.ColumnInfo columnInfo;
                try
                {
                    columnInfo = entityDescriptor[name];
                }
                catch (Exception)
                {
                    columnInfo = null;
                }

                if (columnInfo != null)
                {
                    if (columnInfo.PropertyAccessor.GetCustomAttribute<DoNotAutoMapAttribute>() != null)
                        continue;

                    target = new EntityPropertyAccessor(columnInfo);

                    //detect whether the column is a reference
                    if (columnInfo.ForeignKey)
                        if (propertyInfo.PropertyType.GetTypeInfo().GetCustomAttribute<MapEntityAttribute>() == null)
                            source = new ModelPrimaryKeySource(columnInfo, propertyInfo);
                }
                else
                {
                    PropertyInfo otherPropertyInfo = entityTypeInfo.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                    if (otherPropertyInfo == null)
                        throw new InvalidOperationException($"Property {name} is not found nor as column of the entity nor as a class property");
                    target = new ClassPropertyAccessor(otherPropertyInfo);
                }

                if (source == null)
                    source = new ClassPropertyAccessor(propertyInfo);
                IPropertyMapping mapping = map.For(target);
                mapping.Source = source;
                mapping.MapFlag = attribute.MapFlags;
            }
        }
    }
}

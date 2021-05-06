using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Gehtsoft.EF.Entities
{
    [AttributeUsage(AttributeTargets.Property)]
    public class EntitySerializationOnlyPropertyAttribute : Attribute
    {
        public string Field { get; set; }
    }

    [Serializable]
    public class SerializableEntity : ISerializable
    {
        public SerializableEntity()
        {
        }

        protected SerializableEntity(SerializationInfo info, StreamingContext context)
        {
            Type type = this.GetType();

            foreach (PropertyInfo propertyInfo in type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                EntityPropertyAttribute propertyAttribute = propertyInfo.GetCustomAttribute<EntityPropertyAttribute>();
                if (propertyAttribute != null)
                {
                    if (!propertyAttribute.IgnoreSerialization)
                        propertyInfo.SetValue(this, info.GetValue(propertyAttribute.Field, propertyInfo.PropertyType));
                }
                else
                {
                    EntitySerializationOnlyPropertyAttribute serializationOnlyPropertyAttribute = propertyInfo.GetCustomAttribute<EntitySerializationOnlyPropertyAttribute>();
                    if (serializationOnlyPropertyAttribute != null)
                        propertyInfo.SetValue(this, info.GetValue(serializationOnlyPropertyAttribute.Field, propertyInfo.PropertyType));
                }
            }
        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            Type type = this.GetType();

            foreach (PropertyInfo propertyInfo in type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                EntityPropertyAttribute propertyAttribute = propertyInfo.GetCustomAttribute<EntityPropertyAttribute>();
                if (propertyAttribute != null)
                {
                    if (!propertyAttribute.IgnoreSerialization)
                        info.AddValue(propertyAttribute.Field, propertyInfo.GetValue(this));
                }
                else
                {
                    EntitySerializationOnlyPropertyAttribute serializationOnlyPropertyAttribute = propertyInfo.GetCustomAttribute<EntitySerializationOnlyPropertyAttribute>();
                    if (serializationOnlyPropertyAttribute != null)
                        info.AddValue(serializationOnlyPropertyAttribute.Field, propertyInfo.GetValue(this));
                }
            }
        }
    }

    [Serializable]
    public class SerializableEntityCollection<T> : EntityCollection<T> where T : class
    {
        private T[] mSerializationSource;

        public SerializableEntityCollection()
        {
        }

        [OnDeserialized]
        private void DeserealizationFinished(StreamingContext _context)
        {
            this.AddRange(mSerializationSource);
            mSerializationSource = null;
        }

        protected SerializableEntityCollection(SerializationInfo info, StreamingContext _context)
        {
            mSerializationSource = (T[])info.GetValue("content", typeof(T[]));
        }
    }
}

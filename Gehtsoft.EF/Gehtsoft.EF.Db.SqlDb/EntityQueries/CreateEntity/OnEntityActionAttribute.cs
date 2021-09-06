using System;
using System.Reflection;
using System.Threading.Tasks;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The base class for attribute to set the action when entity or property is created by `CreateEntityController`.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    [DocgenIgnore]
    public abstract class OnEntityActionAttribute : Attribute
    {
        private readonly Type mType;
        private readonly string mName;
        private EntityActionDelegate mAction;
        private bool mInit = false;

        private void Initialize()
        {
            mInit = true;
            mAction = null;

            MethodInfo mi = mType.GetTypeInfo().GetMethod(mName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null)
                throw new ArgumentException($"Action method {mName} isn't found");

            var parameters = mi.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(SqlDbConnection))
                throw new ArgumentException("Action method should have only one parameter and this parameter must accepts Sql connection");

            if (mi.ReturnType == typeof(void))
            {
                mAction = mi.CreateDelegate(typeof(EntityActionDelegate)) as EntityActionDelegate;
            }
            else
            {
                throw new ArgumentException("Action method should return void");
            }

            if (mAction == null)
                throw new ArgumentException("Delegate signature does not match");
        }

        protected EntityActionDelegate Action
        {
            get
            {
                if (!mInit)
                    Initialize();

                return mAction;
            }
        }

        protected OnEntityActionAttribute(Type containerType, string delegateName)
        {
            mType = containerType;
            mName = delegateName;
        }

        public void Invoke(SqlDbConnection connection)
        {
            if (!mInit)
                Initialize();
            mAction?.Invoke(connection);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal interface IParametersHolder
    {
        object LastStatementResult { get; set; }
        bool AddGlobalParameter(string name, SqlConstant value);
        void UpdateGlobalParameter(string name, SqlConstant value);
        SqlConstant FindGlobalParameter(string name);
        bool ContainsGlobalParameter(string name);
    }
}

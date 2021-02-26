﻿using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.OracleDb
{
    class OracleDropViewBuilder : DropViewBuilder
    {
        public OracleDropViewBuilder(SqlDbLanguageSpecifics specifics, string name) : base(specifics, name)
        {

        }

        public override void PrepareQuery()
        {
            StringBuilder builder = new StringBuilder();
            
            builder.Append(mSpecifics.PreBlock);
            builder.Append(mSpecifics.PreQueryInBlock);
            builder.Append($"DROP VIEW {mName}");
            builder.Append(mSpecifics.PostQueryInBlock);
            builder.Append("EXCEPTION\r\n");
            builder.Append("  WHEN OTHERS THEN NULL;\r\n");
            builder.Append(mSpecifics.PostBlock);

            mQuery = builder.ToString();
        }
    }
}

# Why?

Why anyone would bother having yet another Entity Framework when there are one in .NET?

Well...

In short... if you ever tried to:
  * Create DB-agnostic application in Microsoft EF Core...
  * Implement domain-driven design with multiple isolated contexts but pointed to the same database instance
  * And then make the DB update process also DB agnostic
  * And had to spend hours optimizing LINQ created by yet another guy who doesn't understand what is behind LINQ in RDMBS...
  * Yet again update your code because Microsoft decided to drop half of functionality and rename another half in the
    new version.

Then you may got the idea why... We switched to our own solution back in 2015 and never ever looked back because:
  * The framework is 100% pure code-first framework and does NOT require any kind of dependency between the project and the certain database instance. This helps to run the project in distributed environment and even let different developers use different database (not instances, but literally database engines, e.g. Postgres and SQLite) while working on the same project. This also lets use SQLite for running unittests instead of mocking DAO behavior that saves time to develop unit tests and improves the coverage.
  * The framework is 100% database-agnostic and domain-driven-design focused. Create as many context as you need, mix and match them. Use the database driver that matches you needs the best - from in-memory SQLite for automated tests to Oracle for production. And let the framework to take care of all the nuances.
  * While framework supports LINQ, it is heavily oriented to be used without LINQ. The reason is that LINQ is not 100% compatible with SQL select query structure and therefore the LINQ queries compiled into SQL SELECT queries are far from optimal. Gehtsoft.EF framework generates selects which are almost as good as written manually. Unfortunately, due to reason above, LINQ-produced queries are as bad as for any other LINQ-based framework.
  * It is lighter and do not consists of needless dependencies. Actually, for the most queries it is as fast as it would be running these queries directly to the database without EF at all.
  * It allows to use plain and raw SQL. Moreover, it allows to use SQL constructors which creates database-specific SQL SELECTs on the fly.
  * Last but not not least - it is open source. Understand how it works, make it better, make it custom!

# Packages

## Core

|Package|Designation|Links|
|-------|-----------|-----|
|Gehtsoft.EF.Db.Sql|Main Entity Framework Package|[Gehtsoft feed](https://proget.gehtsoft.com/feeds/public-nuget/Gehtsoft.EF.Db.SqlDb/versions), [nuget](https://www.nuget.org/packages/Gehtsoft.EF.Db.SqlDb)
|Gehtsoft.EF.Entities|Entities Definition (use this package if only entities definition is needed)|[Gehtsoft feed](https://proget.gehtsoft.com/feeds/public-nuget/Gehtsoft.EF.Entities/versions), [nuget](https://www.nuget.org/packages/Gehtsoft.EF.Entities)
|Gehtsoft.EF.Utils|Various tools (you don't need to install it, it will be installed automatically)|[Gehtsoft feed](https://proget.gehtsoft.com/feeds/public-nuget/Gehtsoft.EF.Utils/versions), [nuget](https://www.nuget.org/packages/Gehtsoft.EF.Utils)
|Gehtsoft.EF.Bson|BSON support (will be installed automatically if MongoDB driver is used)|[Gehtsoft feed](https://proget.gehtsoft.com/feeds/public-nuget/Gehtsoft.EF.Bson/versions), [nuget](https://www.nuget.org/packages/Gehtsoft.EF.Bson)

## Utils
|Package|Designation|Links|
|-------|-----------|-----|
|Gehtsoft.EF.FTS|Tools to create a full-text-search in your project|[Gehtsoft feed](https://proget.gehtsoft.com/feeds/public-nuget/Gehtsoft.EF.FTS/versions), [nuget](https://www.nuget.org/packages/Gehtsoft.EF.FTS)
|Gehtsoft.EF.Db.SqlDb.OData|OData support|[Gehtsoft feed](https://proget.gehtsoft.com/feeds/public-nuget/Gehtsoft.EF.Db.SqlDb.OData/versions), [nuget](https://www.nuget.org/packages/Gehtsoft.EF.Db.SqlDb.OData)
|Gehtsoft.EF.Db.SqlDb.Sql|Platform-agnostic SQL 92 parser & executor|[Gehtsoft feed](https://proget.gehtsoft.com/feeds/public-nuget/Gehtsoft.EF.Db.SqlDb.Sql/versions), [nuget](https://www.nuget.org/packages/Gehtsoft.EF.Db.SqlDb.Sql)


## Driver
|Package|Designation|Links|
|-------|-----------|-----|
|Gehtsoft.EF.Db.Sql.MssqlDb|Driver for Microsoft SQL Server|[Gehtsoft feed](https://proget.gehtsoft.com/feeds/public-nuget/Gehtsoft.EF.Db.MssqlDb/versions), [nuget](https://www.nuget.org/packages/Gehtsoft.EF.Db.MssqlDb)
|Gehtsoft.EF.Db.Sql.MysqlDb|Driver for MySQL/MariaDB|[Gehtsoft feed](https://proget.gehtsoft.com/feeds/public-nuget/Gehtsoft.EF.Db.MysqlDb/versions), [nuget](https://www.nuget.org/packages/Gehtsoft.EF.Db.MysqlDb)
|Gehtsoft.EF.Db.Sql.OracleDb|Driver for Oracle|[Gehtsoft feed](https://proget.gehtsoft.com/feeds/public-nuget/Gehtsoft.EF.Db.OracleDb/versions), [nuget](https://www.nuget.org/packages/Gehtsoft.EF.Db.OracleDb)
|Gehtsoft.EF.Db.Sql.PostgresDb|Driver for Postgres|[Gehtsoft feed](https://proget.gehtsoft.com/feeds/public-nuget/Gehtsoft.EF.Db.PostgresDb/versions), [nuget](https://www.nuget.org/packages/Gehtsoft.EF.Db.PostgresDb)
|Gehtsoft.EF.Db.Sql.SqliteDb|Driver for Sqlite|[Gehtsoft feed](https://proget.gehtsoft.com/feeds/public-nuget/Gehtsoft.EF.Db.SqliteDb/versions), [nuget](https://www.nuget.org/packages/Gehtsoft.EF.Db.SqliteDb)
|Gehtsoft.EF.Db.MongoDB|Driver for MongoDB|[Gehtsoft feed](https://proget.gehtsoft.com/feeds/public-nuget/Gehtsoft.EF.MongoDB/versions), [nuget](https://www.nuget.org/packages/Gehtsoft.EF.MongoDB)


# See also
  * Read the documentation: https://docs.gehtsoftusa.com/Gehtsoft.EF/ef/
  * Check the source code: https://github.com/gehtsoft-usa/Gehtsoft.EF
  * Use it!
  * Ask a question in discussion
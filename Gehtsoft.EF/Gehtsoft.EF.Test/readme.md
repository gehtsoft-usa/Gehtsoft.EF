# Configuration

## Creating Configuration

The test configuration is kept in `Configuration.json` file. Because this file 
consists of local settings, including database user names and password, it is not
and should not be commuted to the git. 

Use `Configuration.Example.json` as a guide to create your own configuration file. 

If at the moment of the first build the `Configuration.json` file isn't created 
yet, it will be automatically created using `Configuration.Example.json`.

Default configuration defines SQLite source only. 

## Using configuration to manage test process

The most cases that uses the real database will be automatically executed 
for every SQL connection defined in `"sql-connections"` section of the documentation
if they have `driver` and `connectionString` defined and `enabled` set to `true`. 

If you want temporarily limit testing to few databases, use `global-filter:sql-drivers` setting.
If you replace `all` with a comma-separated list of connections and/or drivers, 
the testing will be limited to those drivers only. 
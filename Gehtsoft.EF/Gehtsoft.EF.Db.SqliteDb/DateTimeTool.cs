using System;

namespace Gehtsoft.EF.Db.SqliteDb
{
    public static class DateTimeTool
    {
        public static DateTime FromOADate(double oleDate) => DateTime.FromOADate(oleDate);


        public static double ToOADate(DateTime dateTime) => dateTime.ToOADate();
    }
}

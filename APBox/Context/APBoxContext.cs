using API.Context;
//using MySql.Data.EntityFramework;
using System.Data.Entity;

namespace APBox.Context
{
    [DbConfigurationType(typeof(MySql.Data.Entity.MySqlEFConfiguration))]
    public class APBoxContext : DataContext
    {
    }
}
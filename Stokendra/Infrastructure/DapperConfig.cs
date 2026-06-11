using Dapper;
using System;
using System.Data;

namespace Stokendra.Infrastructure;

/// <summary>
/// Dapper global configuration and custom type handlers for SQLite dynamic typing compatibility.
/// </summary>
public static class DapperConfig
{
    /// <summary>
    /// Initializes custom type handlers and mappings.
    /// </summary>
    public static void Initialize()
    {
        SqlMapper.RemoveTypeMap(typeof(double));
        SqlMapper.AddTypeHandler(new DoubleHandler());

        SqlMapper.RemoveTypeMap(typeof(double?));
        SqlMapper.AddTypeHandler(new NullableDoubleHandler());
    }

    private class DoubleHandler : SqlMapper.TypeHandler<double>
    {
        public override void SetValue(IDbDataParameter parameter, double value)
        {
            parameter.Value = value;
        }

        public override double Parse(object value)
        {
            return Convert.ToDouble(value);
        }
    }

    private class NullableDoubleHandler : SqlMapper.TypeHandler<double?>
    {
        public override void SetValue(IDbDataParameter parameter, double? value)
        {
            parameter.Value = (object?)value ?? DBNull.Value;
        }

        public override double? Parse(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            return Convert.ToDouble(value);
        }
    }
}

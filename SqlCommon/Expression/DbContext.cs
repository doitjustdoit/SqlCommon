﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlCommon.Linq
{
    public enum DbContextState
    {
        Closed = 0,
        Open = 1,
        Commit = 2,
        Rollback = 3,
    }
    public enum DbContextType
    {
        Mysql,
        SqlServer,
        Postgresql,
        Oracle,
        Sqlite,
    }
    public struct DbContextLogger
    {
        public string Text { get; set; }
        public object Value { get; set; }
    }
    public interface IDbContext : IDisposable
    {
        ISqlMapper SqlMapper { get; }
        DbContextState DbContextState { get; }
        DbContextType DbContextType { get; }
        int ExecuteNonQuery(string sql, object param = null, int? commandTimeout = null, CommandType commandType = CommandType.Text);
        Task<int> ExecuteNonQueryAsync(string sql, object param = null, int? commandTimeout = null, CommandType commandType = CommandType.Text);
        T ExecuteScalar<T>(string sql, object param = null, int? commandTimeout = null, CommandType commandType = CommandType.Text);
        Task<T> ExecuteScalarAsync<T>(string sql, object param = null, int? commandTimeout = null, CommandType commandType = CommandType.Text);
        IEnumerable<T> ExecuteQuery<T>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = CommandType.Text);
        (IEnumerable<T1>, IEnumerable<T2>) ExecuteQuery<T1, T2>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = CommandType.Text);
        Task<IEnumerable<T>> ExecuteQueryAsync<T>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = CommandType.Text);
        Task<(IEnumerable<T1>, IEnumerable<T2>)> ExecuteQueryAsync<T1, T2>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null);
        IDbConnection Connection { get; }
        IDbTransaction Transaction { get; }
        void Open(bool beginTransaction = false, IsolationLevel? isolationLevel = null);
        Task OpenAsync(bool beginTransaction = false, IsolationLevel? isolationLevel = null);
        void Commit();
        void Rollback();
        void Close();
    }
    public class DbContext : IDbContext
    {
        public DbContext(IDbConnection connection, DbContextType contextType, ITypeMapper typeMapper=null)
        {
            Connection = connection;
            DbContextType = contextType;
            DbContextState = DbContextState.Closed;
            SqlMapper = new SqlMapper(connection, typeMapper ?? new TypeMapper());
        }
        public DbContext(ISqlMapper sqlMapper, DbContextType contextType)
        {
            Connection = sqlMapper.Connection;
            DbContextType = contextType;
            DbContextState = DbContextState.Closed;
            SqlMapper = sqlMapper;
        }
        public ISqlMapper SqlMapper { get; }
        public DbContextState DbContextState { get; private set; }
        public DbContextType DbContextType { get; private set; }
        public IDbConnection Connection { get; private set; }
        public IDbTransaction Transaction { get; private set; }
        public virtual void Close()
        {
            try
            {
                Connection?.Close();
                Transaction?.Dispose();
            }
            catch (Exception)
            {
            }
            finally
            {
                DbContextState = DbContextState.Closed;
            }
        }
        public virtual void Commit()
        {
            Transaction?.Commit();
            DbContextState = DbContextState.Commit;
        }
        public virtual void Dispose()
        {
            Close();
        }
        public virtual (IEnumerable<T1>, IEnumerable<T2>) ExecuteQuery<T1, T2>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = CommandType.Text)
        {
            return SqlMapper.ExecuteQuery<T1, T2>(sql, param, Transaction, commandTimeout, commandType);
        }
        public virtual Task<(IEnumerable<T1>, IEnumerable<T2>)> ExecuteQueryAsync<T1, T2>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = CommandType.Text)
        {
            return SqlMapper.ExecuteQueryAsync<T1, T2>(sql, param, Transaction, commandTimeout, commandType);
        }
        public virtual Task<int> ExecuteNonQueryAsync(string sql, object param = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
        {
            return SqlMapper.ExecuteNonQueryAsync(sql, param, Transaction, commandTimeout, commandType);
        }
        public virtual int ExecuteNonQuery(string sql, object param = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
        {
            return SqlMapper.ExecuteNonQuery(sql, param, Transaction, commandTimeout, commandType);
        }
        public virtual IEnumerable<T> ExecuteQuery<T>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = CommandType.Text)
        {
            return SqlMapper.ExecuteQuery<T>(sql, param, Transaction, commandTimeout, commandType).ToList();
        }
        public virtual Task<IEnumerable<T>> ExecuteQueryAsync<T>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = CommandType.Text)
        {
            return SqlMapper.ExecuteQueryAsync<T>(sql, param, Transaction, commandTimeout, commandType);
        }
        public virtual T ExecuteScalar<T>(string sql, object param = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
        {
            return SqlMapper.ExecuteScalar<T>(sql, param, Transaction, commandTimeout, commandType);
        }
        public virtual Task<T> ExecuteScalarAsync<T>(string sql, object param = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
        {
            return SqlMapper.ExecuteScalarAsync<T>(sql, param, Transaction, commandTimeout, commandType);
        }
        public virtual void Open(bool beginTransaction = false, IsolationLevel? isolationLevel = null)
        {
            Connection.Open();
            if (beginTransaction)
            {
                Transaction = isolationLevel == null ? Connection.BeginTransaction() : Connection.BeginTransaction(isolationLevel.Value);
            }
            DbContextState = DbContextState.Open;
        }
        public virtual async Task OpenAsync(bool beginTransaction = false, IsolationLevel? isolationLevel = null)
        {
            var dbConnection = Connection as DbConnection;
            await dbConnection.OpenAsync();
            if (beginTransaction)
            {
                Transaction = isolationLevel == null ? dbConnection.BeginTransaction() : Connection.BeginTransaction(isolationLevel.Value);
            }
            DbContextState = DbContextState.Open;
        }
        public virtual void Rollback()
        {
            Transaction?.Rollback();
            DbContextState = DbContextState.Rollback;
        }
    }
    public class DbProxyContext : IDbContext
    {
        private IDbContext DbContext { get; set; }
        public ISqlMapper SqlMapper => DbContext.SqlMapper;
        public DbContextState DbContextState => DbContext.DbContextState;
        public DbContextType DbContextType => DbContext.DbContextType;
        public List<DbContextLogger> Loggers { get; private set; } = new List<DbContextLogger>();
        public IDbConnection Connection => DbContext.Connection;
        public IDbTransaction Transaction => DbContext.Transaction;
        public DbProxyContext(IDbConnection connection, DbContextType contextType)
        {
            DbContext = new DbContext(connection, contextType);
        }
        public DbProxyContext(IDbContext dbContext)
        {
            DbContext = dbContext;
        }
        public int ExecuteNonQuery(string sql, object param = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
        {
            WriteLogger(sql, param);
            return DbContext.ExecuteNonQuery(sql, param, commandTimeout, commandType);
        }
        public Task<int> ExecuteNonQueryAsync(string sql, object param = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
        {
            WriteLogger(sql, param);
            return DbContext.ExecuteNonQueryAsync(sql, param, commandTimeout, commandType);
        }
        public T ExecuteScalar<T>(string sql, object param = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
        {
            WriteLogger(sql, param);
            return DbContext.ExecuteScalar<T>(sql, param, commandTimeout, commandType);
        }
        public Task<T> ExecuteScalarAsync<T>(string sql, object param = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
        {
            WriteLogger(sql, param);
            return DbContext.ExecuteScalarAsync<T>(sql, param, commandTimeout, commandType);
        }
        public IEnumerable<T> ExecuteQuery<T>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = CommandType.Text)
        {
            WriteLogger(sql, param);
            return DbContext.ExecuteQuery<T>(sql, param, commandTimeout, commandType);
        }
        public (IEnumerable<T1>, IEnumerable<T2>) ExecuteQuery<T1, T2>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = CommandType.Text)
        {
            WriteLogger(sql, param);
            return DbContext.ExecuteQuery<T1, T2>(sql, param, commandTimeout, commandType);
        }
        public Task<IEnumerable<T>> ExecuteQueryAsync<T>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = CommandType.Text)
        {
            WriteLogger(sql, param);
            return DbContext.ExecuteQueryAsync<T>(sql, param, commandTimeout, commandType);
        }
        public Task<(IEnumerable<T1>, IEnumerable<T2>)> ExecuteQueryAsync<T1, T2>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            WriteLogger(sql, param);
            return DbContext.ExecuteQueryAsync<T1, T2>(sql, param, commandTimeout, commandType);
        }
        public void Open(bool beginTransaction = false, IsolationLevel? isolationLevel = null)
        {
            DbContext.Open(beginTransaction, isolationLevel);
        }
        public Task OpenAsync(bool beginTransaction = false, IsolationLevel? isolationLevel = null)
        {
            return DbContext.OpenAsync(beginTransaction, isolationLevel);
        }
        public void Commit()
        {
            DbContext.Commit();
        }
        public void Rollback()
        {
            DbContext.Rollback();
        }
        public void Close()
        {
            DbContext.Close();
        }
        public void Dispose()
        {
            DbContext.Dispose();
        }
        private void WriteLogger(string sql, object param)
        {
            Loggers.Add(new DbContextLogger()
            {
                Text = sql,
                Value = param
            });
        }
    }
    public static class DbContextExtension
    {
        public static IQueryable<T> From<T>(this IDbContext dbContext, string viewName) where T : class
        {
            return new SqlQuery<T>(dbContext, viewName);
        }
        public static IQueryable<T> From<T>(this IDbContext dbContext, int? timeout = null) where T : class
        {
            return new SqlQuery<T>(dbContext, null, timeout);
        }
        public static IQueryable<T1, T2> From<T1, T2>(this IDbContext dbContext, string viewName)
            where T1 : class
            where T2 : class
        {
            return new SqlQuery<T1, T2>(dbContext, viewName);
        }
        public static IQueryable<T1, T2> From<T1, T2>(this IDbContext dbContext, int? timeout = null)
         where T1 : class
         where T2 : class
        {
            return new SqlQuery<T1, T2>(dbContext, null, timeout);
        }
        public static IQueryable<T1, T2, T3> From<T1, T2, T3>(this IDbContext dbContext, string viewName)
            where T1 : class
            where T2 : class
            where T3 : class
        {
            return new SqlQuery<T1, T2, T3>(dbContext, viewName);
        }
        public static IQueryable<T1, T2, T3> From<T1, T2, T3>(this IDbContext dbContext, int? timeout = null)
           where T1 : class
           where T2 : class
           where T3 : class
        {
            return new SqlQuery<T1, T2, T3>(dbContext, null, timeout);
        }
        public static IQueryable<T1, T2, T3, T4> From<T1, T2, T3, T4>(this IDbContext dbContext, string viewName)
            where T1 : class
            where T2 : class
            where T3 : class
            where T4 : class
        {
            return new SqlQuery<T1, T2, T3, T4>(dbContext, viewName);
        }
        public static IQueryable<T1, T2, T3, T4> From<T1, T2, T3, T4>(this IDbContext dbContext, int? timeout = null)
           where T1 : class
           where T2 : class
           where T3 : class
           where T4 : class
        {
            return new SqlQuery<T1, T2, T3, T4>(dbContext, null, timeout);
        }
    }
}

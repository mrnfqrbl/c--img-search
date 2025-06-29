// 数据库 库
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace 数据库
{
    // 实体基类，所有数据库实体应继承该类
    public abstract class 实体基类
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
    }

    // 索引类型
    public enum 索引类型
    {
        Search,
        // 其他索引类型
    }
    // MongoDB复合文本索引特性
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class MongoTextIndexAttribute : Attribute
    {
        public string[] FieldNames { get; }
        
        public MongoTextIndexAttribute(params string[] fieldNames)
        {
            FieldNames = fieldNames;
        }
    }
    // MongoDB索引配置特性
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public class MongoIndexAttribute : Attribute
    {
        public string FieldName { get; set; }
        public 索引类型 IndexType { get; set; }

        public MongoIndexAttribute(string fieldName, 索引类型 indexType)
        {
            FieldName = fieldName;
            IndexType = indexType;
        }
    }

    // 数据库服务类，负责启动和连接MongoDB
    public static class 数据库服务
    {
        private static IMongoClient? _client;
        private static IMongoDatabase? _database;
        private static bool _isRunning;
        private static readonly object _lockObject = new object();
        private static string _connectionString = "mongodb://localhost:27017";
        private static string _databaseName = "MyDatabase";

        // 启动数据库服务
        public static void 启动服务(string connectionString = "mongodb://localhost:27017", string 数据库名 = "MyDatabase")
        {
            if (_isRunning) return;

            lock (_lockObject)
            {
                if (_isRunning) return;

                _connectionString = connectionString;
                _databaseName = 数据库名;
                
                try
                {
                    _client = new MongoClient(_connectionString);
                    _database = _client.GetDatabase(_databaseName);
                    
                    // 创建索引
                    创建实体索引();
                    
                    _isRunning = true;
                    Console.WriteLine($"MongoDB服务已启动 - 连接字符串:{connectionString}, 数据库:{数据库名}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"连接MongoDB出错: {ex.Message}");
                    throw;
                }
            }
        }

        // 停止数据库服务
        public static void 停止服务()
        {
            if (!_isRunning) return;
            
            lock (_lockObject)
            {
                if (!_isRunning) return;
                
                _client = null;
                _database = null;
                _isRunning = false;
                Console.WriteLine("MongoDB服务已停止");
            }
        }

        // 获取Mongo数据库实例
        public static IMongoDatabase 获取数据库()
        {
            if (!_isRunning)
                throw new InvalidOperationException("数据库服务未启动");
            return _database!;
        }
        
        // 创建实体类上定义的索引
        private static void 创建实体索引()
        {
            if (_database == null) return;
            
            try
            {
                // 获取所有实体类型
                var entityTypes = Assembly.GetExecutingAssembly().GetTypes()
                    .Where(t => typeof(实体基类).IsAssignableFrom(t) && !t.IsAbstract);
                
                foreach (var type in entityTypes)
                {
                    // 获取集合名称（默认为类型名称）
                    var collectionName = type.Name;
                    
                    // 获取集合
                    var collection = _database.GetCollection<BsonDocument>(collectionName);
                    
                    // 创建索引
                    创建实体索引(type, collection);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建索引时出错: {ex.Message}");
            }
        }
        
        // 为特定实体类型创建索引
        private static void 创建实体索引(Type entityType, IMongoCollection<BsonDocument> collection)
        {
            try
            {
                // 获取类上的索引属性
                var classIndexes = entityType.GetCustomAttributes<MongoIndexAttribute>(true);
                
                foreach (var indexAttr in classIndexes)
                {
                    创建Mongo索引(collection, indexAttr.FieldName, indexAttr.IndexType);
                }
                
                // 获取属性上的索引属性
                var properties = entityType.GetProperties();
                foreach (var property in properties)
                {
                    var propIndexes = property.GetCustomAttributes<MongoIndexAttribute>(true);
                    foreach (var indexAttr in propIndexes)
                    {
                        创建Mongo索引(collection, indexAttr.FieldName, indexAttr.IndexType);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"为 {entityType.Name} 创建索引时出错: {ex.Message}");
            }
        }
        
        // 创建MongoDB索引
        private static void 创建Mongo索引(IMongoCollection<BsonDocument> collection, string fieldName, 索引类型 indexType)
        {
            try
            {
                var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending(fieldName);
                var indexModel = new CreateIndexModel<BsonDocument>(indexKeys);
                
                // 根据索引类型调整
                switch (indexType)
                {
                    case 索引类型.Search:
                        // MongoDB的文本搜索需要特殊索引
                        indexKeys = Builders<BsonDocument>.IndexKeys.Text(fieldName);
                        indexModel = new CreateIndexModel<BsonDocument>(indexKeys);
                        break;
                }
                
                collection.Indexes.CreateOne(indexModel);
                Console.WriteLine($"成功为字段 {fieldName} 创建 {indexType} 索引");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建索引失败 - 字段: {fieldName}, 类型: {indexType}: {ex.Message}");
            }
        }
    }

    // 会话工厂，用于创建数据库会话
    public static class 会话工厂
    {
        // 创建MongoDB仓储实例
        public static I基础仓储<T> 创建仓储<T>() where T : 实体基类
        {
            return new Mongo仓储<T>();
        }
    }

    // 基础仓储接口，定义基本的数据库操作
    public interface I基础仓储<T> where T : 实体基类
    {
        Task<string> 添加(T 实体, CancellationToken cancellationToken = default);
        Task<T?> 获取(string id);
        Task<List<T>> 获取所有();
        Task 更新(T 实体);
        Task 删除(string id);

        // 搜索功能
        Task<List<T>> 搜索所有字段(string 关键词, CancellationToken token = default);
        Task<List<T>> 搜索指定字段(string 关键词, CancellationToken token = default, params string[] 字段名称);
        Task<List<T>> 查询(Expression<Func<T, bool>> filter, CancellationToken token = default);
        Task<List<T>> 多关键词任意匹配搜索(string[] 字段名称, string[] 关键词列表, CancellationToken token = default);
        Task<List<T>> 多关键词精确匹配搜索(string[] 字段名称, string[] 关键词列表, CancellationToken token = default);
    }

    // MongoDB仓储实现
    public class Mongo仓储<T> : I基础仓储<T>, IDisposable where T : 实体基类
    {
        private readonly IMongoCollection<T> _collection;
        private bool _disposed;

        public Mongo仓储()
        {
            var database = 数据库服务.获取数据库();
            var collectionName = GetCollectionName(typeof(T));
            _collection = database.GetCollection<T>(collectionName);
        }

        // 获取集合名称（默认为类名的复数形式）
        private string GetCollectionName(Type documentType)
        {
            return documentType.Name + "s";
        }
        
        public async Task<string> 添加(T 实体, CancellationToken cancellationToken = default)
        {
            try
            {
                await _collection.InsertOneAsync(实体, cancellationToken: cancellationToken);
                Console.WriteLine($"添加到集合：{typeof(T).Name}成功！");
                return 实体.Id!;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\t[数据库错误] 添加数据失败: {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }

        public async Task<T?> 获取(string id)
        {
            return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<T>> 获取所有()
        {
            return await _collection.Find(_ => true).ToListAsync();
        }

        public async Task 更新(T 实体)
        {
            if (string.IsNullOrEmpty(实体.Id))
                throw new ArgumentNullException("实体ID不能为空");
                
            var filter = Builders<T>.Filter.Eq(x => x.Id, 实体.Id);
            var result = await _collection.ReplaceOneAsync(filter, 实体);
            
            if (!result.IsAcknowledged || result.ModifiedCount == 0)
                throw new InvalidOperationException("更新失败，未找到匹配的文档");
        }

        public async Task 删除(string id)
        {
            var filter = Builders<T>.Filter.Eq(x => x.Id, id);
            await _collection.DeleteOneAsync(filter);
        }

        public async Task<List<T>> 搜索所有字段(string 关键词, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(关键词))
                return new List<T>();
                
            var filter = Builders<T>.Filter.Text(关键词);
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<List<T>> 搜索指定字段(string 关键词, CancellationToken cancellationToken = default, params string[] 字段名称)
        {
            if (string.IsNullOrWhiteSpace(关键词) || 字段名称 == null || !字段名称.Any())
                return new List<T>();
                
            var filterDefinition = Builders<T>.Filter.Empty;
            
            foreach (var field in 字段名称)
            {
                var regex = new BsonRegularExpression(关键词, "i");
                filterDefinition |= Builders<T>.Filter.Regex(field, regex);
            }
            
            return await _collection.Find(filterDefinition).ToListAsync(cancellationToken);
        }

        public async Task<List<T>> 查询(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        {
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<List<T>> 多关键词任意匹配搜索(string[] 字段名称, string[] 关键词列表, CancellationToken token = default)
        {
            if (字段名称 == null || !字段名称.Any() || 关键词列表 == null || !关键词列表.Any())
                return new List<T>();
                
            var filterDefinition = Builders<T>.Filter.Empty;
            
            foreach (var keyword in 关键词列表)
            {
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var keywordFilter = Builders<T>.Filter.Empty;
                    
                    foreach (var field in 字段名称)
                    {
                        var regex = new BsonRegularExpression(keyword, "i");
                        keywordFilter |= Builders<T>.Filter.Regex(field, regex);
                    }
                    
                    filterDefinition |= keywordFilter;
                }
            }
            
            return await _collection.Find(filterDefinition).ToListAsync(token);
        }

        public async Task<List<T>> 多关键词精确匹配搜索(string[] 字段名称, string[] 关键词列表, CancellationToken token = default)
        {
            if (字段名称 == null || !字段名称.Any() || 关键词列表 == null || !关键词列表.Any())
                return new List<T>();
                
            var filterDefinition = Builders<T>.Filter.Empty;
            
            foreach (var keyword in 关键词列表)
            {
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var keywordFilter = Builders<T>.Filter.Empty;
                    
                    foreach (var field in 字段名称)
                    {
                        var regex = new BsonRegularExpression(keyword, "i");
                        keywordFilter |= Builders<T>.Filter.Regex(field, regex);
                    }
                    
                    filterDefinition &= keywordFilter;
                }
            }
            
            return await _collection.Find(filterDefinition).ToListAsync(token);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
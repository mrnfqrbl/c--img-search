// 入口程序
using img_info.批量处理;
using 数据库;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace PNGMetadataBatchProcessorExample
{
    // 数据类 (数据模型)，使用MongoDB特性标记索引
    [MongoTextIndex(nameof(数据类.文件名), nameof(数据类.文件路径), nameof(数据类.Description))]
    public class 数据类 : 实体基类
    {
        public string 文件名 { get; set; } = "";
        public string 文件路径 { get; set; } = "";
        public Dictionary<string, string> 元数据 { get; set; } = new Dictionary<string, string>();
        public string Description { get; set; } = "";
    }

    class Program
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static int _searchMode = 1; // 默认搜索模式：1-单关键词全部字段

        static async Task Main(string[] args)
        {
            Console.WriteLine("启动第一时间");
            
            // 1. 解析命令行参数
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            List<string> 目录列表 = new List<string>();
            bool 递归搜索 = false;
            bool 格式化元数据 = false;
            string? 搜索关键词 = null;
            string[]? 搜索字段 = null;

            Console.WriteLine("解析命令行参数:");
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                Console.WriteLine($"  参数 {i}: {arg}");

                if (arg.Equals("-r", StringComparison.OrdinalIgnoreCase))
                {
                    递归搜索 = true;
                    Console.WriteLine("  设置递归搜索: true");
                }
                else if (arg.Equals("-f", StringComparison.OrdinalIgnoreCase))
                {
                    格式化元数据 = true;
                    Console.WriteLine("  设置格式化元数据: true");
                }
                else if (arg.Equals("-s", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        搜索关键词 = args[i + 1];
                        Console.WriteLine($"  解析到搜索关键词: {搜索关键词}");
                        i++;

                        // 检查是否有搜索模式参数 -m
                        if (i + 1 < args.Length && args[i].Equals("-m", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(args[i + 1], out int mode) && mode >= 1 && mode <= 4)
                            {
                                _searchMode = mode;
                                Console.WriteLine($"  已设置搜索模式: {_searchMode} - {GetSearchModeName(_searchMode)}");
                                i += 2; // 跳过 -m 和模式值
                            }
                            else
                            {
                                Console.WriteLine("  警告：无效的搜索模式，使用默认模式1");
                                i++; // 跳过 -m
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  未指定搜索模式，使用默认模式: {_searchMode}");
                        }

                        // 检查是否有搜索字段
                        if (i + 1 < args.Length && !args[i].StartsWith("-"))
                        {
                            string 搜索字段1 = args[i + 1];
                            搜索字段 = 搜索字段1.Split('|');
                            Console.WriteLine($"  解析到搜索字段: {string.Join(", ", 搜索字段)}");
                            i++;
                        }
                        else
                        {
                            Console.WriteLine("  警告：缺少搜索字段，将根据模式选择默认字段");
                            搜索字段 = null;
                        }
                    }
                    else
                    {
                        Console.WriteLine("错误：缺少搜索关键词。");
                        PrintUsage();
                        return;
                    }
                }
                else
                {
                    目录列表.Add(arg);
                    Console.WriteLine($"  添加目录: {arg}");
                }
            }

            // 2. 初始化MongoDB服务
            数据库服务.启动服务("mongodb://mrnf:mrnfqrbl@192.168.1.115:27019", "png元数据");

            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            try
            {
                // 3. 创建MongoDB仓储实例
                I基础仓储<数据类> 数据仓储 = 会话工厂.创建仓储<数据类>();

                // 4. 执行操作：处理目录或搜索
                if (!string.IsNullOrEmpty(搜索关键词))
                {
                    // 执行数据库搜索
                    Console.WriteLine($"--- 搜索数据库---关键词：{搜索关键词} ---模式：{GetSearchModeName(_searchMode)} ---");
                    await 搜索数据库(数据仓储, 搜索关键词, 搜索字段, _searchMode, token);
                }
                else
                {
                    // 执行批量处理
                    批量图片元数据处理器 批量处理器 = new 批量图片元数据处理器();
                    Console.WriteLine("--- 使用异步 + 回调函数处理 ---");

                    var 文件和元数据迭代器 = 批量处理器.异步处理多个目录(目录列表, 递归搜索, 格式化元数据, token);

                    await Task.Run(async () =>
                    {
                        try
                        {
                            await foreach (var (filePath, metadata) in 文件和元数据迭代器.WithCancellation(token))
                            {
                                if (token.IsCancellationRequested)
                                {
                                    Console.WriteLine("处理被取消。");
                                    break;
                                }

                                Console.WriteLine($"[文件] {filePath}");
                                var 字符串元数据 = metadata.ToDictionary(
                                    kvp => kvp.Key,
                                    kvp => kvp.Value?.ToString() ?? ""
                                );
                                数据类 数据 = new 数据类
                                {
                                    文件名 = Path.GetFileName(filePath),
                                    文件路径 = filePath,
                                    元数据 = 字符串元数据,
                                    Description = (string)(metadata.ContainsKey("Description") ? 
                                        metadata["Description"] : 
                                        (metadata.ContainsKey("iTXt.Description") ? 
                                        metadata["iTXt.Description"] : "")
                                    )
                                };

                                try
                                {
                                    await 数据仓储.添加(数据, token);
                                    Console.WriteLine($"\t[数据库] 数据已保存: {数据.Id},");
                                }
                                catch (OperationCanceledException ex)
                                {
                                    Console.WriteLine($"\t[数据库错误] 添加操作被取消: {ex.Message}");
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"\t[数据库错误] 添加数据失败: {ex.GetType().Name} - {ex.Message}");
                                    if (ex.InnerException != null)
                                    {
                                        Console.WriteLine($"\t[数据库错误] 内部异常: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                                    }
                                }

                                foreach (var kvp in metadata)
                                {
                                    Console.WriteLine($"\t{kvp.Key}: {kvp.Value}");
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine("任务被外部取消。");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"发生错误: {ex.Message}");
                        }
                        finally
                        {
                            批量处理器.停止();
                            Console.WriteLine("异步处理完成。\n");
                        }
                    }, token);

                    Console.WriteLine("等待处理任务完成...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误: {ex.Message}");
            }
            finally
            {
                数据库服务.停止服务();
                Console.WriteLine("数据库连接已关闭");
            }

            Console.WriteLine("over");
        }

        // 搜索数据库的辅助方法（适配MongoDB）
        static async Task 搜索数据库(I基础仓储<数据类> 数据仓储, string 关键词, string[]? 搜索字段, int 搜索模式, CancellationToken token)
        {
            Console.WriteLine($"实际执行的搜索模式: {搜索模式} - {GetSearchModeName(搜索模式)}");
            
            List<数据类> searchResults = new List<数据类>();
            
            try
            {
                switch (搜索模式)
                {
                    case 1: // 单关键词全部字段
                        searchResults = await 数据仓储.搜索所有字段(关键词, token);
                        Console.WriteLine("使用模式1: 单关键词全部字段搜索");
                        break;
                        
                    case 2: // 单关键词指定字段
                        searchResults = await 数据仓储.搜索指定字段(关键词, token, 搜索字段 ?? Array.Empty<string>());
                        Console.WriteLine($"使用模式2: 单关键词指定字段搜索，字段: {string.Join(", ", 搜索字段 ?? Array.Empty<string>())}");
                        break;
                        
                    case 3: // 多关键词任意匹配
                        if (搜索字段 == null || !搜索字段.Any())
                        {
                            搜索字段 = new[] { "文件名", "文件路径", "Description" }; // 默认搜索字段
                        }
                        string[] 关键词列表 = 关键词.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        searchResults = await 数据仓储.多关键词任意匹配搜索(搜索字段, 关键词列表, token);
                        Console.WriteLine($"使用模式3: 多关键词任意匹配搜索，字段: {string.Join(", ", 搜索字段)}, 关键词: {string.Join(", ", 关键词列表)}");
                        break;
                        
                    case 4: // 多关键词精确匹配
                        if (搜索字段 == null || !搜索字段.Any())
                        {
                            搜索字段 = new[] { "文件名", "文件路径", "Description" }; // 默认搜索字段
                        }
                        string[] 精确关键词列表 = 关键词.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        searchResults = await 数据仓储.多关键词精确匹配搜索(搜索字段, 精确关键词列表, token);
                        Console.WriteLine($"使用模式4: 多关键词精确匹配搜索，字段: {string.Join(", ", 搜索字段)}, 关键词: {string.Join(", ", 精确关键词列表)}");
                        break;
                        
                    default:
                        Console.WriteLine("无效的搜索模式，使用默认模式1");
                        searchResults = await 数据仓储.搜索所有字段(关键词, token);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"搜索过程中发生错误: {ex.Message}");
                return;
            }

            if (searchResults.Count != 0)
            {
                Console.WriteLine($"找到 {searchResults.Count} 个匹配项:");
                foreach (var data in searchResults)
                {
                    Console.WriteLine($"\t文件名: {data.文件名}, 文件路径: {data.文件路径}, Id: {data.Id}");
                    Console.WriteLine($"\tDescription: {data.Description}");
                    Console.WriteLine("\t元数据:");
                    foreach (var kvp in data.元数据)
                    {
                        Console.WriteLine($"\t\t{kvp.Key}: {kvp.Value}");
                    }
                    Console.WriteLine("---");
                }
            }
            else
            {
                Console.WriteLine("未找到匹配项。");
            }
        }

        // 获取搜索模式名称
        static string GetSearchModeName(int mode)
        {
            return mode switch
            {
                1 => "单关键词全部字段",
                2 => "单关键词指定字段",
                3 => "多关键词任意匹配",
                4 => "多关键词精确匹配",
                _ => "未知模式"
            };
        }

        // 打印用法说明
        static void PrintUsage()
        {
            Console.WriteLine("用法： PNGMetadataBatchProcessorExample <目录1> [<目录2> ...] [-r] [-f] [-s <关键词> [-m <模式>] [<字段1>|<字段2>...]]");
            Console.WriteLine("  <目录1> [<目录2> ...]: 要处理的目录路径列表。");
            Console.WriteLine("  -r: 递归搜索子目录 (可选)。");
            Console.WriteLine("  -f: 格式化元数据 (可选)。");
            Console.WriteLine("  -s <关键词> [-m <模式>] [<字段1>|<字段2>...]: 从数据库搜索");
            Console.WriteLine("    -m <模式>: 搜索模式 (1-单关键词全部字段, 2-单关键词指定字段, 3-多关键词任意匹配, 4-多关键词精确匹配)");
            Console.WriteLine("    [<字段1>|<字段2>...]: 搜索字段，用'|'分隔 (模式2-4时有效)");
        }
    }
}
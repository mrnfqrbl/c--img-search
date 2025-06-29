using System;
using img_info.图片处理类库; // 引用 img_info 的命名空间
using System.Collections.Generic;
using System.IO;
using System.Linq;
using 并发库; // 引用并发库的命名空间
using System.Threading.Tasks;

namespace DebugConsole
{
    class 程序
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("调试控制台启动!"); // 输出调试信息

            // 获取命令行参数 (跳过程序路径)
            string[] 命令行参数 = Environment.GetCommandLineArgs();
            // 删除第一个元素 (程序路径)
            List<string> 参数列表 = new List<string>(命令行参数);
            参数列表.RemoveAt(0);
            命令行参数 = 参数列表.ToArray();

            // 解析命令行参数
            Dictionary<string, string> 参数字典 = 解析命令行参数(命令行参数);

            // 检查是否有命令行参数
            if (参数字典.Count > 0)
            {
                Console.WriteLine("接收到的命令行参数:"); // 输出调试信息
                foreach (var 键值对 in 参数字典)
                {
                    Console.WriteLine($"  {键值对.Key}: {键值对.Value}"); // 输出调试信息
                }

                // 在 DebugConsole 中处理命令行参数
                if (参数字典.ContainsKey("get-metadata"))
                {
                    // 优先使用--dir指定的目录，如果没有则尝试提取文件名
                    string 目录 = 参数字典.ContainsKey("dir") ? 参数字典["dir"] : "";
                    int 递归深度 = 参数字典.ContainsKey("d") ? int.Parse(参数字典["d"]) : 0; // 默认不递归
                    int 最大并行度 = 参数字典.ContainsKey("p") ? int.Parse(参数字典["p"]) : -1; // 默认使用 CPU 核心数

                    // 检查是否提供了目录参数，如果没提供尝试提取文件名
                    if (string.IsNullOrEmpty(目录))
                    {
                        // 检查是否提供了一个参数，并且该参数是否为文件路径
                        string 文件名 = 获取文件名(参数字典);
                        if (!string.IsNullOrEmpty(文件名))
                        {
                            // 单文件处理模式
                            Console.WriteLine($"进入单文件处理模式，文件: {文件名}");
                            await 处理单个文件(文件名); // 直接处理单个文件，不使用并发
                        }
                        else
                        {
                            Console.WriteLine("请使用 --dir 参数指定目录，或在 get-metadata 命令后直接提供文件路径。");
                            return;
                        }
                    }
                    else
                    {
                        // 目录处理模式
                        if (!Directory.Exists(目录))
                        {
                            Console.WriteLine($"目录 \"{目录}\" 不存在。");
                            return;
                        }

                        List<string> 文件列表 = 获取文件列表(目录, "*.png", 递归深度); // 获取PNG文件列表

                        // 创建并发引擎
                        并发引擎 引擎 = new 并发引擎(最大并行度);

                        // 添加任务到并发引擎
                        foreach (string 文件名 in 文件列表)
                        {
                            string 当前文件名 = 文件名;
                            引擎.添加任务(async () =>
                            {
                                Console.WriteLine($"正在处理文件: {当前文件名}，线程ID: {Thread.CurrentThread.ManagedThreadId}");
                                try
                                {
                                    图片元数据处理器 元数据处理器 = new 图片元数据处理器();
                                    Dictionary<string, object> 元数据 = 元数据处理器.读取Png元数据(当前文件名,true); // 调用 img_info 类库的方法

                                    if (元数据.Count > 0)
                                    {
                                        Console.WriteLine("元数据:"); // 输出调试信息
                                        foreach (var 键值对 in 元数据)
                                        {
                                            Console.WriteLine($"  {键值对.Key}: {键值对.Value}"); // 输出元数据信息
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("未找到元数据或发生错误。"); // 输出调试信息
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"处理文件 \"{当前文件名}\" 时发生错误: {ex.Message}");
                                }
                            });
                        }

                        // 等待所有任务完成
                        引擎.等待所有任务完成();

                        // 停止并发引擎
                        引擎.停止();
                    }
                }
                else
                {
                    Console.WriteLine("未知命令。"); // 输出调试信息
                }
            }
            else
            {
                Console.WriteLine("未提供任何命令行参数。"); // 输出调试信息
            }

            Console.WriteLine("调试控制台结束!"); // 输出调试信息
        }

        // 处理单个文件
        static async Task 处理单个文件(string 文件名)
        {
            Console.WriteLine($"正在处理单个文件: {文件名}");
            try
            {
                图片元数据处理器 元数据处理器 = new 图片元数据处理器();
                Dictionary<string, object> 元数据 = 元数据处理器.读取Png元数据(文件名,true); // 调用 img_info 类库的方法

                if (元数据.Count > 0)
                {
                    Console.WriteLine("元数据:"); // 输出调试信息
                    foreach (var 键值对 in 元数据)
                    {
                        Console.WriteLine($"  {键值对.Key}: {键值对.Value}"); // 输出元数据信息
                    }
                }
                else
                {
                    Console.WriteLine("未找到元数据或发生错误。"); // 输出调试信息
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理文件 \"{文件名}\" 时发生错误: {ex.Message}");
            }
        }

        // 从参数字典中提取文件名（忽略参数顺序）
        static string 获取文件名(Dictionary<string, string> 参数字典)
        {
            foreach (var 键值对 in 参数字典)
            {
                if (键值对.Key != "get-metadata" && File.Exists(键值对.Key))
                {
                    return 键值对.Key;
                }
            }
            return null;
        }


        // 解析命令行参数
    
        static Dictionary<string, string> 解析命令行参数(string[] args)
        {
            Dictionary<string, string> 参数字典 = new Dictionary<string, string>();

            // 先存储命令本身
            if (args.Length > 0)
            {
                参数字典[args[0]] = ""; // 存储命令
            }

            for (int i = 1; i < args.Length; i++)  // 从索引 1 开始，跳过命令
            {
                string arg = args[i];

                if (arg.StartsWith("--") || arg.StartsWith("-"))
                {
                    // 长选项 或 短选项
                    string 键;
                    string 值 = "";

                    int 等号索引 = arg.IndexOf('=');
                    if (等号索引 > 0)
                    {
                        // 存在等号，分割键和值
                        键 = arg.Substring(arg.StartsWith("--") ? 2 : 1, 等号索引 - (arg.StartsWith("--") ? 2 : 1));
                        值 = arg.Substring(等号索引 + 1);
                    }
                    else
                    {
                        // 不存在等号，只有键，值可能是下一个参数
                        键 = arg.Substring(arg.StartsWith("--") ? 2 : 1);
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            值 = args[i + 1];
                            i++; // 跳过值
                        }
                    }

                    参数字典[键] = 值;
                }
                else
                {
                    // 假设是文件名或目录名（无键参数）
                    参数字典[args[i]] = "";
                }
            }

            return 参数字典;
        }

        // 获取文件列表（递归）
        static List<string> 获取文件列表(string 目录, string 搜索模式, int 递归深度)
        {
            List<string> 文件列表 = new List<string>();
            try
            {
                文件列表.AddRange(Directory.GetFiles(目录, 搜索模式));

                if (递归深度 != 0) // 0 表示不递归
                {
                    string[] 子目录 = Directory.GetDirectories(目录);
                    foreach (string 子目录路径 in 子目录)
                    {
                        int 下一级递归深度 = (递归深度 > 0) ? 递归深度 - 1 : -1; // 如果递归深度是正数，则递减，否则保持-1（无限递归）
                        文件列表.AddRange(获取文件列表(子目录路径, 搜索模式, 下一级递归深度));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取文件列表时发生错误: {ex.Message}");
            }

            return 文件列表;
        }
    }
}




// using System;
// using img_info.图片处理类库; // 引用 img_info 的命名空间
// using System.Collections.Generic;

// namespace DebugConsole
// {
//     class 程序
//     {
//         static void Main(string[] args)
//         {
//             Console.WriteLine("调试控制台启动!"); // 输出调试信息

//             // 获取命令行参数 (跳过程序路径)
//             string[] 命令行参数 = Environment.GetCommandLineArgs();
//             // 删除第一个元素 (程序路径)
//             List<string> 参数列表 = new List<string>(命令行参数);
//             参数列表.RemoveAt(0);
//             命令行参数 = 参数列表.ToArray();

//             // 检查是否有命令行参数
//             if (命令行参数.Length > 0)
//             {
//                 Console.WriteLine("接收到的命令行参数:"); // 输出调试信息
//                 foreach (string 参数 in 命令行参数)
//                 {
//                     Console.WriteLine(参数); // 输出调试信息
//                 }

//                 // 在 DebugConsole 中处理命令行参数
//                 if (命令行参数[0] == "get-metadata")
//                 {
//                     string 文件名 = 命令行参数[1]; // 假设第二个参数是文件名
//                     图片元数据处理器 元数据处理器 = new 图片元数据处理器();
//                     Dictionary<string, object> 元数据 = 元数据处理器.读取Png元数据(文件名); // 调用 img_info 类库的方法

//                     if (元数据.Count > 0)
//                     {
//                         Console.WriteLine("元数据:"); // 输出调试信息
//                         foreach (var 键值对 in 元数据)
//                         {
//                             Console.WriteLine($"  {键值对.Key}: {键值对.Value}"); // 输出元数据信息
//                         }
//                     }
//                     else
//                     {
//                         Console.WriteLine("未找到元数据或发生错误。"); // 输出调试信息
//                     }
//                 }
//                 else
//                 {
//                     Console.WriteLine("未知命令。"); // 输出调试信息
//                 }
//             }
//             else
//             {
//                 Console.WriteLine("未提供任何命令行参数。"); // 输出调试信息
//             }

//             Console.WriteLine("调试控制台结束!"); // 输出调试信息
//         }
//     }
// }

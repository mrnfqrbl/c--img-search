using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using img_info.图片处理类库;
using 并发库;
using System.Threading.Channels;

namespace img_info.批量处理
{
    /// <summary>
    /// 批量PNG图片元数据处理类。
    /// </summary>
    public class 批量图片元数据处理器
    {
        private readonly 并发引擎 _并发引擎;

        /// <summary>
        /// 构造函数，可以指定并发数量。
        /// </summary>
        /// <param name="最大并发度">最大并发处理线程数，默认为处理器核心数。</param>
        public 批量图片元数据处理器()
        {
            _并发引擎 = new 并发引擎();
        }

        /// <summary>
        /// 处理指定目录下的所有PNG图片，并返回包含元数据的迭代器。
        /// </summary>
        /// <param name="目录路径">要处理的目录路径。</param>
        /// <param name="递归搜索">是否递归搜索子目录。</param>
        /// <param name="格式化元数据">是否格式化元数据。</param>
        /// <returns>包含元数据的迭代器。</returns>
        public IEnumerable<Tuple<string, Dictionary<string, object>>> 处理目录(string 目录路径, bool 递归搜索 = false, bool 格式化元数据 = false)
        {
            if (!Directory.Exists(目录路径))
            {
                Console.WriteLine($"目录不存在：{目录路径}");
                yield break; // 返回空的迭代器
            }

            SearchOption searchOption = 递归搜索 ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] 文件路径 = Directory.GetFiles(目录路径, "*.png", searchOption);

            foreach (string 文件 in 文件路径)
            {
                // 使用迭代器yield返回结果
                yield return 处理单个文件(文件, 格式化元数据);
            }
        }

        /// <summary>
        /// 处理多个目录下的所有PNG图片，并返回包含元数据的迭代器。
        /// </summary>
        /// <param name="目录路径列表">要处理的目录路径列表。</param>
        /// <param name="递归搜索">是否递归搜索子目录。</param>
        /// <param name="格式化元数据">是否格式化元数据。</param>
        /// <returns>包含元数据的迭代器。</returns>
        public IEnumerable<Tuple<string, Dictionary<string, object>>> 处理多个目录(IEnumerable<string> 目录路径列表, bool 递归搜索 = false, bool 格式化元数据 = false)
        {
            foreach (string 目录 in 目录路径列表)
            {
                foreach (var 结果 in 处理目录(目录, 递归搜索, 格式化元数据))
                {
                    yield return 结果;
                }
            }
        }

        /// <summary>
        /// 处理单个 PNG 图片文件，并返回包含元数据的元组。
        /// </summary>
        /// <param name="文件路径">PNG 图片的文件路径。</param>
        /// <param name="格式化元数据">是否格式化元数据。</param>
        /// <returns>包含文件路径和元数据的元组。</returns>
        private Tuple<string, Dictionary<string, object>> 处理单个文件(string 文件路径, bool 格式化元数据)
        {
            图片元数据处理器 元数据处理器 = new 图片元数据处理器();
            Dictionary<string, object> 元数据 = 元数据处理器.读取Png元数据(文件路径, 格式化元数据);
            return Tuple.Create(文件路径, 元数据);
        }

        /// <summary>
        /// 异步处理指定目录下的所有PNG图片，并返回一个异步迭代器。
        /// </summary>
        /// <param name="目录路径">要处理的目录路径。</param>
        /// <param name="递归搜索">是否递归搜索子目录。</param>
        /// <param name="格式化元数据">是否格式化元数据。</param>
        /// <returns>包含元数据的异步迭代器。</returns>
        public async IAsyncEnumerable<Tuple<string, Dictionary<string, object>>> 异步处理目录(string 目录路径, bool 递归搜索 = false, bool 格式化元数据 = false, CancellationToken cancellationToken = default)
        {
            var channel = Channel.CreateUnbounded<Tuple<string, Dictionary<string, object>>>();

            if (!Directory.Exists(目录路径))
            {
                Console.WriteLine($"目录不存在：{目录路径}");
                yield break;
            }

            SearchOption searchOption = 递归搜索 ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] 文件路径 = Directory.GetFiles(目录路径, "*.png", searchOption);

            Task producer = Task.Run(async () =>
            {
                try
                {
                    foreach (string 文件 in 文件路径)
                    {
                        cancellationToken.ThrowIfCancellationRequested(); // 检查取消

                        string 当前文件 = 文件; // 避免闭包问题
                        Dictionary<string, object> 元数据 = null;

                        try
                        {
                            图片元数据处理器 元数据处理器 = new 图片元数据处理器();
                            元数据 = 元数据处理器.读取Png元数据(当前文件, 格式化元数据);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"处理文件 {当前文件} 时发生错误: {ex.Message}");
                            // 可选：将错误信息写入通道，例如写入一个特殊标记或 null 值
                            // await channel.Writer.WriteAsync(Tuple.Create(当前文件, (Dictionary<string, object>)null));
                            continue; // 继续处理下一个文件
                        }

                        await channel.Writer.WriteAsync(Tuple.Create(当前文件, 元数据), cancellationToken).ConfigureAwait(false); // 传递 token
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("生产者任务被取消。");
                }
                finally
                {
                    channel.Writer.Complete(); // 标记完成
                }
            }, cancellationToken); // 传递 CancellationToken

            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
        }

        /// <summary>
        /// 异步处理多个目录下的所有PNG图片，并返回一个异步迭代器。
        /// </summary>
        /// <param name="目录路径列表">要处理的目录路径列表。</param>
        /// <param name="递归搜索">是否递归搜索子目录。</param>
        /// <param name="格式化元数据">是否格式化元数据。</param>
        /// <returns>包含元数据的异步迭代器。</returns>
        public async IAsyncEnumerable<Tuple<string, Dictionary<string, object>>> 异步处理多个目录(IEnumerable<string> 目录路径列表, bool 递归搜索 = false, bool 格式化元数据 = false,CancellationToken cancellationToken = default)
        {
            foreach (string 目录 in 目录路径列表)
            {
                await foreach (var 结果 in 异步处理目录(目录, 递归搜索, 格式化元数据, cancellationToken).WithCancellation(cancellationToken))
                {
                    yield return 结果;
                }
            }
        }

          /// <summary>
        /// 等待所有任务完成。
        /// </summary>
        public void 等待所有任务完成()
        {
            _并发引擎.等待所有任务完成();
        }

        /// <summary>
        /// 停止并发引擎。
        /// </summary>
        public void 停止()
        {
             _并发引擎.停止();
        }

         /// <summary>
        /// 停止并发引擎，强制停止所有任务。
        /// </summary>
        public void 停止(bool 强制停止)
        {
             _并发引擎.停止(强制停止);
        }
    }
}

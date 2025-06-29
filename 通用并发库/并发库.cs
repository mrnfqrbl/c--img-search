using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace 并发库
{
    /// <summary>
    /// 通用并发库，支持多线程和异步任务。
    /// </summary>
    public class 并发引擎
    {
        private readonly int _最大并行度; // 最大并发任务数
        private readonly TaskScheduler _任务调度器; // 任务调度器
        private readonly BlockingCollection<Func<Task>> _任务队列 = new BlockingCollection<Func<Task>>(); // 任务队列，用于存储待执行的任务
        private readonly CancellationTokenSource _取消令牌源 = new CancellationTokenSource(); // 取消令牌源，用于取消所有任务

        private static AsyncLocal<Guid> _请求ID = new AsyncLocal<Guid>(); // 用于在异步任务间传递请求 ID

        /// <summary>
        /// 初始化并发引擎。
        /// </summary>
        /// <param name="最大并行度">最大并发任务数。默认为处理器核心数。</param>
        public 并发引擎(int 最大并行度 = -1)
        {
            int 预留线程数 = 2; // 预留 2 个线程，防止线程饥饿
            _最大并行度 = 最大并行度 > 0 ? 最大并行度 : Math.Max(1, Environment.ProcessorCount - 预留线程数); // 保证至少有一个线程可用, 默认使用CPU核心数
            _任务调度器 = new LimitedConcurrencyLevelTaskScheduler(_最大并行度); // 使用自定义的任务调度器，限制并发数

            // 启动工作线程
            for (int i = 0; i < _最大并行度; i++)
            {
                Task.Factory.StartNew(
                    async () =>
                    {
                        try
                        {
                            foreach (var 任务 in _任务队列.GetConsumingEnumerable(_取消令牌源.Token))
                            {
                                try
                                {
                                    await 任务(); // 执行任务
                                }
                                catch (OperationCanceledException)
                                {
                                    // 任务被取消
                                    Console.WriteLine("任务被取消.");
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    // 捕获任务中的异常，并记录日志
                                    Console.WriteLine($"任务执行出错: {ex.Message}");
                                    // 可选：将异常传递给调用方或进行重试等操作
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // 在 GetConsumingEnumerable 中捕获 OperationCanceledException
                            Console.WriteLine("工作线程已取消。");
                        }
                    },
                    _取消令牌源.Token,
                    TaskCreationOptions.LongRunning, //  指示任务是长时间运行的任务
                    TaskScheduler.Default  // 使用默认调度器启动
                );
            }
            Console.WriteLine($"并发引擎已启动，最大并行度：{_最大并行度}");
        }

        /// <summary>
        /// 添加一个任务到队列中等待执行。
        /// </summary>
        /// <param name="任务">要执行的任务委托。</param>
        public void 添加任务(Func<Task> 任务)
        {
            if (任务 == null)
            {
                throw new ArgumentNullException(nameof(任务), "任务不能为空");
            }
            try
            {
                _任务队列.Add(任务);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"添加任务失败: {ex.Message}");
                // 可选：重新抛出异常或进行其他处理
            }
        }
        /// <summary>
        /// 添加一个任务到队列中等待执行。
        /// </summary>
        /// <param name="任务">要执行的任务委托。</param>
        /// <param name="请求ID">请求ID</param>
        public void 添加任务(Func<Task> 任务, Guid 请求ID)
        {
            if (任务 == null)
            {
                throw new ArgumentNullException(nameof(任务), "任务不能为空");
            }
            _任务队列.Add(async () =>
            {
                _请求ID.Value = 请求ID;
                Console.WriteLine($"任务开始执行，请求ID: {_请求ID.Value}");
                try
                {
                    await 任务();
                }
                finally
                {
                    Console.WriteLine($"任务完成，请求ID: {_请求ID.Value}");
                    _请求ID.Value = Guid.Empty; // 清空请求ID
                }
            });
        }

        /// <summary>
        /// 添加一个同步任务到队列中等待执行。
        /// </summary>
        /// <param name="任务">要执行的同步任务委托。</param>
        public void 添加任务(Action 任务)
        {
            if (任务 == null)
            {
                throw new ArgumentNullException(nameof(任务), "任务不能为空");
            }
            _任务队列.Add(() => Task.Run(任务)); // 将同步任务包装成异步任务
        }

        /// <summary>
        /// 等待所有任务完成。
        /// </summary>
        public void 等待所有任务完成()
        {
            _任务队列.CompleteAdding(); // 标记添加完成

            // 使用 Task.WhenAll 来等待所有任务完成
            try
            {
                // 创建一个 Task 列表来跟踪所有工作线程
                List<Task> 工作线程任务列表 = new List<Task>();
                for (int i = 0; i < _最大并行度; i++)
                {
                    Task 工作线程任务 = Task.Run(async () =>
                    {
                        try
                        {
                            foreach (var 任务 in _任务队列.GetConsumingEnumerable(_取消令牌源.Token))
                            {
                                try
                                {
                                    await 任务(); // 执行任务
                                }
                                catch (OperationCanceledException)
                                {
                                    // 任务被取消
                                    Console.WriteLine("任务被取消.");
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    // 捕获任务中的异常
                                    Console.WriteLine($"任务执行出错: {ex.Message}");
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // 在 GetConsumingEnumerable 中捕获 OperationCanceledException
                            Console.WriteLine("工作线程已取消。");
                        }
                    });
                    工作线程任务列表.Add(工作线程任务);
                }

                // 等待所有工作线程完成
                Task.WaitAll(工作线程任务列表.ToArray());
            }
            catch (AggregateException ae)
            {
                // 处理 AggregateException 异常，例如记录所有内部异常
                foreach (var ex in ae.InnerExceptions)
                {
                    Console.WriteLine($"一个或多个任务执行出错: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                // 处理其他异常
                Console.WriteLine($"等待任务完成时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止并发引擎，尽可能等待所有任务完成。
        /// </summary>
        public void 停止()
        {
            停止(false); // 调用带参数的停止方法，默认非强制停止
        }


        /// <summary>
        /// 停止并发引擎。
        /// </summary>
        /// <param name="强制停止">如果为 true，则强制停止所有任务，否则等待任务完成。</param>
        public void 停止(bool 强制停止)
        {
            Console.WriteLine($"并发引擎开始停止，强制停止: {强制停止}");
            _任务队列.CompleteAdding(); // 停止添加新任务

            if (强制停止)
            {
                // 强制停止：取消所有未完成的任务
                _取消令牌源.Cancel();
                Console.WriteLine("已发出取消信号，所有任务将被强制停止。");
            }

            try
            {
                // 创建一个 Task 列表来跟踪所有工作线程
                List<Task> 工作线程任务列表 = new List<Task>();
                for (int i = 0; i < _最大并行度; i++)
                {
                    Task 工作线程任务 = Task.Run(async () =>
                    {
                        try
                        {
                            foreach (var 任务 in _任务队列.GetConsumingEnumerable(_取消令牌源.Token))
                            {
                                try
                                {
                                    await 任务(); // 执行任务
                                }
                                catch (OperationCanceledException)
                                {
                                    // 任务被取消
                                    Console.WriteLine("任务被取消.");
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    // 捕获任务中的异常
                                    Console.WriteLine($"任务执行出错: {ex.Message}");
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // 在 GetConsumingEnumerable 中捕获 OperationCanceledException
                            Console.WriteLine("工作线程已取消。");
                        }
                    });
                    工作线程任务列表.Add(工作线程任务);
                }

                // 等待所有工作线程完成
                try
                {
                    Task.WaitAll(工作线程任务列表.ToArray());
                }
                catch (AggregateException ae)
                {
                    // 处理 AggregateException 异常，例如记录所有内部异常
                    foreach (var ex in ae.InnerExceptions)
                    {
                        Console.WriteLine($"一个或多个任务执行出错: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    // 处理其他异常
                    Console.WriteLine($"等待任务完成时发生错误: {ex.Message}");
                }
            }
            finally
            {
                Console.WriteLine("并发引擎已停止。");
            }
        }

        /// <summary>
        ///  一个任务调度器，用于限制并发级别。
        /// </summary>
        private class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
        {
            [ThreadStatic]
            private static bool _当前线程正在执行; // 表示当前线程是否正在执行任务。

            private readonly LinkedList<Task> _任务队列; // 任务队列。
            private readonly int _最大并发级别; // 最大并发级别。
            private int _当前并发数; // 当前并发数。

            /// <summary>
            /// 初始化新的 LimitedConcurrencyLevelTaskScheduler 实例。
            /// </summary>
            /// <param name="最大并发级别">并发级别上限。</param>
            public LimitedConcurrencyLevelTaskScheduler(int 最大并发级别)
            {
                if (最大并发级别 < 1) throw new ArgumentOutOfRangeException("最大并发级别 不能小于 1");
                _最大并发级别 = 最大并发级别;
                _任务队列 = new LinkedList<Task>();
            }

            /// <summary>
            /// 将提供的任务加入到调度器的队列中。
            /// </summary>
            /// <param name="任务">要排队的任务。</param>
            protected sealed override void QueueTask(Task 任务)
            {
                lock (_任务队列)
                {
                    _任务队列.AddLast(任务);
                    if (_当前并发数 < _最大并发级别)
                    {
                        _当前并发数++;
                        NotifyThreadPoolOfPendingWork();
                    }
                }
            }

            /// <summary>
            /// 通知线程池有挂起的任务需要处理。
            /// </summary>
            private void NotifyThreadPoolOfPendingWork()
            {
                ThreadPool.UnsafeQueueUserWorkItem(_ =>
                {
                    // 注意，此 lambda 可能同时在多个线程上运行
                    // 因此，我们需要确保我们只执行一个线程
                    // 在任何时候。
                    try
                    {
                        _当前线程正在执行 = true;
                        while (true)
                        {
                            Task nextTask;
                            lock (_任务队列)
                            {
                                // 当没有更多要处理的任务时，从工作线程中取出
                                if (_任务队列.Count == 0)
                                {
                                    _当前并发数--;
                                    break;
                                }

                                // 获取下一个任务以执行，并将其从队列中移除
                                nextTask = _任务队列.First.Value;
                                _任务队列.RemoveFirst();
                            }

                            // 执行任务
                            try
                            {
                                TryExecuteTask(nextTask);
                            }
                            catch (Exception ex)
                            {
                                //处理任务执行期间发生的异常
                                Console.WriteLine($"任务执行期间发生异常: {ex.Message}");
                            }
                        }
                    }
                    finally { _当前线程正在执行 = false; }
                }, null);
            }

            /// <summary>
            /// 确定提供的任务是否可以同步执行。
            /// </summary>
            /// <param name="task">将要执行的任务。</param>
            /// <param name="taskWasPreviouslyQueued"></param>
            /// <returns>
            /// 如果任务可以在当前线程上执行，则为 True；否则为 False。
            /// </returns>
            protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            {
                // 如果此线程当前正在运行任务，请不要内联它。
                return _当前线程正在执行 && TryExecuteTask(task);
            }

            /// <summary>
            /// 返回与此 TaskScheduler 关联的最大并发级别。
            /// </summary>
            public sealed override int MaximumConcurrencyLevel { get { return _最大并发级别; } }

            /// <summary>
            /// 获取当前排队等待运行的任务的可枚举对象。
            /// </summary>
            /// <returns>可枚举对象，该对象允许枚举等待运行的任务。</returns>
            protected sealed override IEnumerable<Task> GetScheduledTasks()
            {
                bool lockTaken = false;
                try
                {
                    Monitor.TryEnter(_任务队列, ref lockTaken);
                    if (lockTaken) return _任务队列.ToArray();
                    else throw new NotSupportedException();
                }
                finally
                {
                    if (lockTaken) Monitor.Exit(_任务队列);
                }
            }
        }
    }
}

// 示例用法
// public class 示例
// {
//     private static AsyncLocal<Guid> _请求ID = new AsyncLocal<Guid>();
//     public static  Task Main(string[] args)
//     {

//         // 创建一个并发引擎，限制最大并发数为 4
//         并发库.并发引擎 引擎 = new 并发库.并发引擎(4);

//         // 添加一些任务
//         for (int i = 0; i < 10; i++)
//         {
//             int 任务ID = i; // 避免闭包问题
//             Guid 请求ID = Guid.NewGuid();
//             //使用AsyncLocal传递请求ID
//             _请求ID.Value = 请求ID;

//             引擎.添加任务(async () =>
//             {
//                 Console.WriteLine($"任务 {任务ID} 开始执行，线程ID: {Thread.CurrentThread.ManagedThreadId}，请求ID: {_请求ID.Value}");
//                 await Task.Delay(Random.Shared.Next(500, 2000)); // 模拟耗时操作
//                 Console.WriteLine($"任务 {任务ID} 执行完毕，线程ID: {Thread.CurrentThread.ManagedThreadId}，请求ID: {_请求ID.Value}");
//             }, 请求ID);
//         }

//         // 添加一个CPU密集型任务
//         引擎.添加任务(() =>
//         {
//             Console.WriteLine($"CPU密集型任务开始执行，线程ID: {Thread.CurrentThread.ManagedThreadId}，请求ID: {_请求ID.Value}");
//             double result = 0;
//             for (int i = 0; i < 100000000; i++)
//             {
//                 result += Math.Sqrt(i);
//             }
//             Console.WriteLine($"CPU密集型任务执行完毕，结果: {result}，线程ID: {Thread.CurrentThread.ManagedThreadId}，请求ID: {_请求ID.Value}");
//         });


//         //等待所有任务完成
//         引擎.等待所有任务完成();
//         Console.WriteLine("所有任务执行完毕.");

//         // 停止引擎 (尝试优雅停止)
//         引擎.停止();

//          // 再次停止引擎 (强制停止)
//          引擎.停止(true);
//     }
// }

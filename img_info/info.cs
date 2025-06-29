using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace img_info.图片处理类库
{
    /// <summary>
    /// PNG图片元数据处理器，仅读取元数据，不加载完整图像。
    /// </summary>
    public class 图片元数据处理器
    {
        /// <summary>
        /// 读取 PNG 图片元数据，提取关键属性信息。
        /// </summary>
        /// <param name="图片路径">PNG 图片的文件路径。</param>
        /// <param name="格式化元数据">是否格式化元数据，如果为true，则返回扁平化的键值对，否则返回原始的，带有块类型前缀的键值对</param>
        /// <returns>包含元数据的字典，键为块类型或属性名，值为属性值。若出错或无元数据则返回空字典。</returns>
        public Dictionary<string, object> 读取Png元数据(string 图片路径, bool 格式化元数据 = false)
        {
            var 原始元数据字典 = new Dictionary<string, object>(); // 初始化原始元数据字典
            Console.WriteLine($"开始读取 PNG 元数据，文件路径: {图片路径}"); // 添加调试输出

            try
            {
                using (var 文件流 = new FileStream(图片路径, FileMode.Open, FileAccess.Read, FileShare.Read)) // 使用FileStream提升性能
                {
                    Console.WriteLine($"文件流已打开，准备读取 PNG 块: {图片路径}"); // 添加调试输出

                    // 验证 PNG 文件头
                    byte[] png签名 = new byte[8];
                    文件流.Read(png签名, 0, 8);
                    if (!ByteArrayCompare(png签名, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                    {
                        Console.WriteLine("文件不是有效的 PNG 文件。"); // 添加调试输出
                        return 原始元数据字典; // 返回空的元数据字典
                    }

                    // 读取和解析 PNG 块
                    while (文件流.Position < 文件流.Length)
                    {
                        // 读取块长度 (4 字节，大端序)
                        byte[] 长度字节 = new byte[4];
                        int readBytes = 文件流.Read(长度字节, 0, 4);
                        if (readBytes < 4)
                        {
                            Console.WriteLine("文件提前结束，无法读取块长度。");
                            break; // 退出循环
                        }
                        uint 块长度 = 从大端序字节数组转换为无符号整数(长度字节);

                        // 读取块类型 (4 字节，ASCII)
                        byte[] 类型字节 = new byte[4];
                        readBytes = 文件流.Read(类型字节, 0, 4);
                        if (readBytes < 4)
                        {
                            Console.WriteLine("文件提前结束，无法读取块类型。");
                            break; // 退出循环
                        }
                        string 块类型 = Encoding.ASCII.GetString(类型字节);

                        // Console.WriteLine($"读取到块，类型: {块类型}，长度: {块长度}"); // 添加调试输出

                        // 读取块数据 (长度由块长度指定)
                        // 使用checked关键字检查是否会溢出
                        try
                        {
                            checked
                            {
                                // 尝试将 uint 转换为 int
                                int 块长度Int = (int)块长度;

                                byte[] 数据 = new byte[块长度Int];
                                readBytes = 文件流.Read(数据, 0, 块长度Int);
                                if (readBytes < 块长度Int)
                                {
                                    Console.WriteLine("文件提前结束，无法读取完整块数据。");
                                    break; // 退出循环
                                }

                                // 读取 CRC (4 字节)
                                byte[] crcBytes = new byte[4];
                                readBytes = 文件流.Read(crcBytes, 0, 4);
                                if (readBytes < 4)
                                {
                                    Console.WriteLine("文件提前结束，无法读取CRC。");
                                    break; // 退出循环
                                }
                                uint crc = 从大端序字节数组转换为无符号整数(crcBytes);

                                //  TODO:  可以计算 CRC 并进行校验，这里省略

                                // 处理块数据
                                处理Png块(块类型, 数据, 原始元数据字典);

                                // 如果是IEND块，结束循环
                                if (块类型 == "IEND")
                                {
                                    Console.WriteLine("读取到IEND块，PNG文件读取结束。"); // 添加调试输出
                                    break; // 退出循环
                                }
                            }
                        }
                        catch (OverflowException)
                        {
                            Console.WriteLine("块长度超出int的最大值，无法处理此块。");
                            //根据实际情况决定是否继续处理后续块或直接返回
                            break; //退出循环
                        }

                    }
                }

                Console.WriteLine("PNG 块读取完成，返回元数据字典。"); // 添加调试输出

                if (格式化元数据)
                {
                     return 格式化元数据字典(原始元数据字典);
                }
                else
                {
                    return 原始元数据字典; // 返回元数据字典
                }

            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"文件未找到异常，文件路径: {图片路径}"); // 添加调试输出
                return 原始元数据字典; // 返回空的元数据字典
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"IO 异常: {ioEx.Message}");
                Console.WriteLine($"StackTrace: {ioEx.StackTrace}");
                return 原始元数据字典; // 返回空的元数据字典
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生异常: {ex.Message}"); // 添加调试输出
                Console.WriteLine($"StackTrace: {ex.StackTrace}"); // 添加调试输出
                return 原始元数据字典; // 返回空的元数据字典
            }
        }

          /// <summary>
        /// 格式化元数据字典，去除块类型前缀，并合并重复的键。
        /// </summary>
        /// <param name="原始元数据字典">原始元数据字典。</param>
        /// <returns>格式化后的元数据字典。</returns>
        private Dictionary<string, object> 格式化元数据字典(Dictionary<string, object> 原始元数据字典)
        {
            var 格式化后的元数据 = new Dictionary<string, object>();

            foreach (var kvp in 原始元数据字典)
            {
                string 键 = kvp.Key;

                // 去除块类型前缀
                if (键.StartsWith("tEXt:") || 键.StartsWith("iTXt:") || 键.StartsWith("zTXt:") || 键.StartsWith("pHYs:"))
                {
                    键 = 键.Substring(5); // 移除前缀
                }

                // 添加到格式化后的字典，如果键已存在，则跳过
                if (!格式化后的元数据.ContainsKey(键))
                {
                    格式化后的元数据[键] = kvp.Value;
                }
            }

            return 格式化后的元数据;
        }


        /// <summary>
        /// 处理 PNG 块数据，提取信息。
        /// </summary>
        /// <param name="块类型">块类型字符串。</param>
        /// <param name="数据">块数据字节数组。</param>
        /// <param name="元数据字典">元数据字典。</param>
        private void 处理Png块(string 块类型, byte[] 数据, Dictionary<string, object> 元数据字典)
        {
            // Console.WriteLine($"开始处理 PNG 块，类型: {块类型}"); // 添加调试输出

            try
            {
                switch (块类型)
                {
                    case "IHDR": // 图像头
                        解析IHDR(数据, 元数据字典);
                        break;

                    case "tEXt": // 文本信息
                        解析文本块(数据, 元数据字典);
                        break;

                    case "iTXt": // 国际文本信息
                        解析国际文本块(数据, 元数据字典);
                        break;

                    case "zTXt": // 压缩文本信息
                        解析压缩文本块(数据, 元数据字典);
                        break;
                    case "pHYs": // 物理像素尺寸
                        解析物理像素尺寸块(数据, 元数据字典);
                        break;
                    // 其他块类型可以根据需要添加

                    case "IDAT": // 图像数据块, 忽略
                        // Console.WriteLine("IDAT 块，忽略解析。");
                        break;

                    case "IEND": // 图像结束块，忽略
                        // Console.WriteLine("IEND 块，忽略解析。");
                        break;

                    default:
                        Console.WriteLine($"未知的块类型: {块类型}，忽略解析。"); // 添加调试输出
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理块时发生异常（类型: {块类型}）: {ex.Message}"); // 添加调试输出
                Console.WriteLine($"StackTrace: {ex.StackTrace}"); // 添加调试输出
            }
        }

        /// <summary>
        /// 解析 IHDR 块，提取图像基本信息。
        /// </summary>
        /// <param name="数据">IHDR 块数据。</param>
        /// <param name="元数据字典">元数据字典。</param>
        private void 解析IHDR(byte[] 数据, Dictionary<string, object> 元数据字典)
        {
            // Console.WriteLine("开始解析 IHDR 块。"); // 添加调试输出

            // 宽度 (4 字节)
            uint 宽度 = 从大端序字节数组转换为无符号整数(数据, 0);
            元数据字典["宽度"] = 宽度;
            // Console.WriteLine($"图像宽度: {宽度}"); // 添加调试输出

            // 高度 (4 字节)
            uint 高度 = 从大端序字节数组转换为无符号整数(数据, 4);
            元数据字典["高度"] = 高度;
            // Console.WriteLine($"图像高度: {高度}"); // 添加调试输出

            // 位深度 (1 字节)
            byte 位深度 = 数据[8];
            元数据字典["位深度"] = 位深度;
            // Console.WriteLine($"位深度: {位深度}"); // 添加调试输出

            // 颜色类型 (1 字节)
            byte 颜色类型 = 数据[9];
            元数据字典["颜色类型"] = 颜色类型;
            // Console.WriteLine($"颜色类型: {颜色类型}"); // 添加调试输出

            // 压缩方法 (1 字节)
            byte 压缩方法 = 数据[10];
            元数据字典["压缩方法"] = 压缩方法;
            // Console.WriteLine($"压缩方法: {压缩方法}"); // 添加调试输出

            // 过滤方法 (1 字节)
            byte 过滤方法 = 数据[11];
            元数据字典["过滤方法"] = 过滤方法;
            // Console.WriteLine($"过滤方法: {过滤方法}"); // 添加调试输出

            // 隔行扫描方法 (1 字节)
            byte 隔行扫描方法 = 数据[12];
            元数据字典["隔行扫描方法"] = 隔行扫描方法;
            // Console.WriteLine($"隔行扫描方法: {隔行扫描方法}"); // 添加调试输出
        }

        /// <summary>
        /// 解析 tEXt 块，提取文本信息（关键字和文本）。
        /// </summary>
        /// <param name="数据">tEXt 块数据。</param>
        /// <param name="元数据字典">元数据字典。</param>
        private void 解析文本块(byte[] 数据, Dictionary<string, object> 元数据字典)
        {
            // Console.WriteLine("开始解析 tEXt 块。"); // 添加调试输出

            // 找到关键字的结束位置 (null 字符)
            int 分隔符索引 = Array.IndexOf(数据, (byte)0);

            if (分隔符索引 > 0)
            {
                // 提取关键字
                string 关键字 = Encoding.UTF8.GetString(数据, 0, 分隔符索引);
                // Console.WriteLine($"文本关键字: {关键字}"); // 添加调试输出

                // 提取文本内容
                string 文本 = Encoding.UTF8.GetString(数据, 分隔符索引 + 1, 数据.Length - 分隔符索引 - 1);
                // Console.WriteLine($"tEXt 块文本内容: {文本}"); // 添加调试输出

                元数据字典[$"tEXt:{关键字}"] = 文本; // 使用 "tEXt:" 前缀，避免与其他块的键冲突
            }
            else
            {
                Console.WriteLine("tEXt 块格式不正确，缺少分隔符。"); // 添加调试输出
            }
        }

        /// <summary>
        /// 解析 iTXt 块，提取国际化文本信息。
        /// </summary>
        /// <param name="数据">iTXt 块数据。</param>
        /// <param name="元数据字典">元数据字典。</param>
        private void 解析国际文本块(byte[] 数据, Dictionary<string, object> 元数据字典)
        {
            // Console.WriteLine("开始解析 iTXt 块。");

            // 关键字以 null 结尾
            int 关键字结束索引 = Array.IndexOf(数据, (byte)0);
            if (关键字结束索引 < 0)
            {
                Console.WriteLine("iTXt 块格式错误：缺少关键字结束符。");
                return;
            }
            string 关键字 = Encoding.UTF8.GetString(数据, 0, 关键字结束索引);
            // Console.WriteLine($"iTXt 关键字: {关键字}");

            // 压缩标志 (1 字节)
            int 当前索引 = 关键字结束索引 + 1;
            byte 压缩标志 = 数据[当前索引++];

            // 压缩方法 (1 字节)
            byte 压缩方法 = 数据[当前索引++];

            // 语言标签以 null 结尾
            int 语言标签结束索引 = Array.IndexOf(数据, (byte)0, 当前索引);
            if (语言标签结束索引 < 0)
            {
                Console.WriteLine("iTXt 块格式错误：缺少语言标签结束符。");
                return;
            }
            string 语言标签 = Encoding.UTF8.GetString(数据, 当前索引, 语言标签结束索引 - 当前索引);
            // Console.WriteLine($"iTXt 语言标签: {语言标签}");

            当前索引 = 语言标签结束索引 + 1;

            // 翻译后的关键字以 null 结尾
            int 翻译关键字结束索引 = Array.IndexOf(数据, (byte)0, 当前索引);
            if (翻译关键字结束索引 < 0)
            {
                Console.WriteLine("iTXt 块格式错误：缺少翻译关键字结束符。");
                return;
            }
            string 翻译关键字 = Encoding.UTF8.GetString(数据, 当前索引, 翻译关键字结束索引 - 当前索引);
            // Console.WriteLine($"iTXt 翻译关键字: {翻译关键字}");

            当前索引 = 翻译关键字结束索引 + 1;

            // 文本内容
            string 文本内容;
            if (压缩标志 == 0)
            {
                // 未压缩
                文本内容 = Encoding.UTF8.GetString(数据, 当前索引, 数据.Length - 当前索引);
            }
            else
            {
                // 压缩，这里简化处理，不实际解压缩
                文本内容 = "压缩数据 (未解压)";
            }
            // Console.WriteLine($"iTXt 文本内容: {文本内容}");

            元数据字典[$"iTXt:{关键字}"] = 文本内容;

        }

        /// <summary>
        /// 解析 zTXt 块，提取压缩文本信息。
        /// </summary>
        /// <param name="数据">zTXt 块数据。</param>
        /// <param name="元数据字典">元数据字典。</param>
        private void 解析压缩文本块(byte[] 数据, Dictionary<string, object> 元数据字典)
        {
            // Console.WriteLine("开始解析 zTXt 块。");

            // 关键字以 null 结尾
            int 关键字结束索引 = Array.IndexOf(数据, (byte)0);
            if (关键字结束索引 < 0)
            {
                Console.WriteLine("zTXt 块格式错误：缺少关键字结束符。");
                return;
            }
            string 关键字 = Encoding.UTF8.GetString(数据, 0, 关键字结束索引);
            // Console.WriteLine($"zTXt 关键字: {关键字}");

            // 压缩方法 (1 字节)
            int 当前索引 = 关键字结束索引 + 1;
            byte 压缩方法 = 数据[当前索引++];

            // 压缩数据 (剩余部分)
            //  TODO: 实际应该使用 DeflateStream 解压缩
            byte[] 压缩数据 = new byte[数据.Length - 当前索引];
            Array.Copy(数据, 当前索引, 压缩数据, 0, 压缩数据.Length);

            // Console.WriteLine("zTXt 内容: 压缩数据 (未解压)");

            元数据字典[$"zTXt:{关键字}"] = "压缩数据 (未解压)";
        }

        /// <summary>
        ///  解析 pHYs 块，提取图像的物理像素尺寸信息
        /// </summary>
        /// <param name="数据">pHYs 块数据.</param>
        /// <param name="元数据字典">元数据字典.</param>
        private void 解析物理像素尺寸块(byte[] 数据, Dictionary<string, object> 元数据字典)
        {
            // Console.WriteLine("开始解析 pHYs 块.");

            // 每像素的像素 (4 字节)
            uint 像素X = 从大端序字节数组转换为无符号整数(数据, 0);
            uint 像素Y = 从大端序字节数组转换为无符号整数(数据, 4);
            byte 单位 = 数据[8];

            // Console.WriteLine($"像素X: {像素X}, 像素Y: {像素Y}, 单位: {单位}");

            元数据字典["pHYs:像素X"] = 像素X;
            元数据字典["pHYs:像素Y"] = 像素Y;
            元数据字典["pHYs:单位"] = 单位;
        }

        /// <summary>
        /// 比较两个字节数组是否相等。
        /// </summary>
        /// <param name="a1">第一个字节数组。</param>
        /// <param name="a2">第二个字节数组。</param>
        /// <returns>如果相等则返回 true，否则返回 false。</returns>
        private bool ByteArrayCompare(byte[] a1, byte[] a2)
        {
            if (a1.Length != a2.Length)
                return false;

            for (int i = 0; i < a1.Length; i++)
                if (a1[i] != a2[i])
                    return false;

            return true;
        }

        /// <summary>
        /// 从大端序字节数组转换为无符号整数。
        /// </summary>
        /// <param name="字节数组">字节数组。</param>
        /// <param name="startIndex">起始索引，默认为0</param>
        /// <returns>无符号整数。</returns>
        private uint 从大端序字节数组转换为无符号整数(byte[] 字节数组, int startIndex = 0)
        {
            return (uint)(字节数组[startIndex] << 24 | 字节数组[startIndex + 1] << 16 | 字节数组[startIndex + 2] << 8 | 字节数组[startIndex + 3]);
        }

        /// <summary>
        /// 从大端序字节数组转换为无符号整数。
        /// </summary>
        /// <param name="字节数组">字节数组。</param>
        /// <returns>无符号整数。</returns>
        private uint 从大端序字节数组转换为无符号整数(byte[] 字节数组)
        {
            return 从大端序字节数组转换为无符号整数(字节数组, 0);
        }
    }
}

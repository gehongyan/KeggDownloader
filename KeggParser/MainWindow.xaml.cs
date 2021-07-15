using KeggParser.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace KeggParser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 单链接执行处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ParseButton_Click(object sender, RoutedEventArgs e)
        {
            // 禁用按钮避免重复按下
            ParseButton.IsEnabled = false;
            ParsePatchButton.IsEnabled = false;

            try
            {
                // 解析代码
                string url = UrlTextBox.Text;
                List<string> codeList = Utils.KeggParser.ParseLinkToCodes(url);

                // 创建异步任务列表
                List<Task<(string, string)>> tasks = codeList.Select(Utils.KeggParser.ParseCodeToSequenceNumberAsync).ToList();

                while (tasks.Any())
                {
                    // 当有任务完成时
                    Task<(string, string)> completedTask = await Task.WhenAny(tasks);
                    tasks.Remove(completedTask);

                    (string code, string seq) = await completedTask;

                    // 打印结果
                    ResultTextBox.Text += $"{code},{seq}\n";
                }

                ResultTextBox.Text += "===============================\n";
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.StackTrace);
            }

            // 恢复按钮禁用
            ParseButton.IsEnabled = true;
            ParsePatchButton.IsEnabled = true;
        }

        /// <summary>
        /// 批量链接处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ParsePatchButton_Click(object sender, RoutedEventArgs e)
        {
            ParseButton.IsEnabled = false;
            ParsePatchButton.IsEnabled = false;
            try
            {
                // 提取代码列表
                ResultTextBox.Text += "正在提取代码列表\n";
                string url = UrlTextBox.Text;
                List<PathwayStruct> list = await Utils.KeggParser.ParseCatalogAsync(url);

                // 查找5200所在位置
                int filteredIndex = list.FindIndex(p => p.Id == 5200);

                // 滤除5200之后的条目，按数量排序，取前10多
                List<PathwayStruct> tenMaxList =
                    list.Take(filteredIndex)
                        .OrderByDescending(p => p.Count)
                        .Take(10)
                        .ToList();

                // 解析编码并合并
                List<string> codeList = tenMaxList
                    .SelectMany(p => Utils.KeggParser.ParseLinkToCodes(p.Url))
                    .ToList();

                int codeTotal = codeList.Count;
                ResultTextBox.Text += $"获得代码共{codeTotal}个\n";

                // 编码去重
                codeList = codeList.Distinct().ToList();
                int codeDistinct = codeList.Count;
                ResultTextBox.Text += $"代码去重共{codeDistinct}个\n";

                // 分组，每组10个，避免上百请求同时发起
                ResultTextBox.Text += $"正在分组拉取结果\n";
                List<List<string>> codeSeparated = new();

                int j = 10;
                for (int i = 0; i < codeList.Count; i += 10)
                {
                    List<string> cList = codeList.Take(j).Skip(i).ToList();
                    j += 10;
                    codeSeparated.Add(cList);
                }

                // 代码与编码结果字典
                Dictionary<string, string> dic = new();

                for (int index = 0; index < codeSeparated.Count; index++)
                {
                    // 解析编码到序号
                    ResultTextBox.Text += $"第{index + 1}组/共{codeSeparated.Count}组\n";
                    List<Task<(string, string)>> tasks = codeSeparated[index].Select(Utils.KeggParser.ParseCodeToSequenceNumberAsync).ToList();

                    while (tasks.Any())
                    {
                        Task<(string, string)> completedTask = await Task.WhenAny(tasks);
                        tasks.Remove(completedTask);

                        try
                        {
                            // 添加字典
                            (string code, string seq) = await completedTask;
                            bool succeed = dic.TryAdd(code, seq);
                            if (!succeed)
                            {
                                ResultTextBox.Text += $"向字典添加{code},{seq}失败\n";
                            }
                        }
                        catch (Exception exception)
                        {
                            ResultTextBox.Text += $"查询过程中出现错误，请在检查输出结果\n";
                            MessageBox.Show(exception.StackTrace);
                        }
                    }
                }
                ResultTextBox.Text += $"结果拉取完成，正在输出结果\n";

                // 前十条重解析解析
                foreach (PathwayStruct pathwayStruct in tenMaxList)
                {
                    ResultTextBox.Text += $"页面1：{pathwayStruct.Id:D5} ({pathwayStruct.Url})\n";
                    ResultTextBox.Text += $"共{pathwayStruct.Count}条\n";

                    List<string> codes = Utils.KeggParser.ParseLinkToCodes(pathwayStruct.Url);

                    // 查找字典
                    foreach (string code in codes)
                    {
                        if (dic.ContainsKey(code))
                        {
                            if (!string.IsNullOrWhiteSpace(dic[code]))
                            {
                                ResultTextBox.Text += $"{code},{dic[code]}\n";
                            }
                            else
                            {
                                ResultTextBox.Text += $"拉取{code}对应结果匹配空，请手动核实 https://www.kegg.jp/entry/{code}\n";
                            }
                        }
                        else
                        {
                            ResultTextBox.Text += $"拉取{code}对应结果未成功，请手动核实 https://www.kegg.jp/entry/{code}\n";
                        }

                    }
                }

            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.StackTrace);
            }
            ParseButton.IsEnabled = true;
            ParsePatchButton.IsEnabled = true;

        }

    }
}

using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KeggParser.Utils
{
    public static class KeggParser
    {
        /// <summary>
        /// 解析链接到编码列表
        /// </summary>
        /// <param name="link">链接</param>
        /// <returns>编码列表</returns>
        public static List<string> ParseLinkToCodes(string link)
        {
            return link.Remove(0, 88).Split('/').Distinct().ToList();
        }

        /// <summary>
        /// 异步解析编码代码到顺序号
        /// </summary>
        /// <param name="code">
        /// <para>编码</para>
        /// <example>K12345</example>
        /// </param>
        /// <returns>
        /// <para>顺序号</para>
        /// <example>1.2.3.4</example>
        /// </returns>
        public static async Task<(string, string)> ParseCodeToSequenceNumberAsync(string code)
        {
            // 文件存在则返回本地缓存
            string filePath = Path.Combine(AppContext.BaseDirectory, "Data", $"{code}.data");
            if (File.Exists(filePath))
            {
                string localData = await File.ReadAllTextAsync(filePath);
                return (code, localData);
            }

            // 否则联网下载存储后返回
            HtmlWeb webpage = new();
            HtmlDocument webDoc = await webpage.LoadFromWebAsync($"https://www.kegg.jp/entry/{code}");
            string docHtml = webDoc.DocumentNode.InnerText;

            // 匹配形如 [EC:1.2.3.4] 的内容
            Regex rgx = new(@"\[EC:((\d+\.){3}\d+)\]");
            Match result = rgx.Match(docHtml);
            string sequence = result.Groups[1].Value;

            // 写本地缓存
            await File.WriteAllTextAsync(filePath, sequence);

            return (code, sequence);
        }

        /// <summary>
        /// 解析数量
        /// </summary>
        /// <param name="text">文本条目</param>
        /// <returns>末尾括号中的数据量</returns>
        private static int ParseCount(string text)
        {
            Regex rgx = new(@"\((\d+)\)");
            Match match = rgx.Match(text);
            string count = match.Groups[1].Value;
            return int.Parse(count);
        }

        /// <summary>
        /// 解析目录页到页面列表
        /// </summary>
        /// <param name="url">目录页地址</param>
        /// <returns>页面列表</returns>
        public static async Task<List<PathwayStruct>> ParseCatalogAsync(string url)
        {
            HtmlWeb webpage = new();
            HtmlDocument webDoc = await webpage.LoadFromWebAsync(url);
            HtmlNodeCollection nodes = webDoc.DocumentNode.SelectNodes(@"//*[@id=""main""]/p/a");   // XPath

            List<PathwayStruct> list = nodes.Select(n => new PathwayStruct()
            {
                Url = n.Attributes["href"].Value,
                Count = ParseCount(n.NextSibling.InnerText),
                Id = int.Parse(n.InnerText)
            }).ToList();
            return list;
        }
    }


    /// <summary>
    /// Pathway结构
    /// </summary>
    public struct PathwayStruct
    {
        /// <summary>
        /// 网址
        /// </summary>
        public string Url;

        /// <summary>
        /// ID
        /// </summary>
        public int Id;

        /// <summary>
        /// 数量
        /// </summary>
        public int Count;
    }

}
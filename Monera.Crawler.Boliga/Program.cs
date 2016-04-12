using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Transactions;
using HtmlAgilityPack;
using ShellProgressBar;

namespace Monera.Crawler.Boliga
{
    class Program
    {
        private static string _startUrl;
        private static string _siteUrl;
        private static Guid _searchGuid;
        private static List<int> _pageNumbers;
        private static int _totalPropertiesCount;
        private static int _counter;
        private static CultureInfo _danishCulture;
        private static readonly string[] ProcentStr = { "% " };
        private static readonly string[] BrStr = { "<br>" };
        private static readonly string[] StrongBrStr = { "</strong><br>" };
        private static readonly string[] BrRn = { "<br>\r\n" };
        private static readonly string[] Small = { "<small>" };
        private static readonly string[] Krm = { "kr./m&sup2;" };
        private static readonly string[] Msup2 = { "m&sup2;" };
        private static readonly string[] Afialt = { " af ialt " };
        private static ProgressBar _pbar;

        private static int GetTotalPropertiesCount()
        {
            int result;

            using (var client = new HttpClient())
            {
                var uri = new Uri($"{_startUrl}");

                var response = client.GetAsync(uri).Result;
                var responseString = response.Content.ReadAsStringAsync().Result;

                HtmlDocument htmlDoc = new HtmlDocument { OptionFixNestedTags = true };
                htmlDoc.LoadHtml(responseString);

                HtmlNode hn =
                    htmlDoc.DocumentNode.SelectSingleNode("//table[@class='searchresultpaging'][1]/tr/td[2]/label[1]/p");
                string totalPropertiesCountStr =
                    hn?.InnerHtml
                        .Split(Afialt, StringSplitOptions.None)[1]
                        .Split(' ')[0]
                        .Trim();

                result = totalPropertiesCountStr != null
                    ? int.Parse(totalPropertiesCountStr, NumberStyles.Number, _danishCulture)
                    : 0;
            }

            return result;
        }

        private static List<int> GetNewSearchPageNumbers(HtmlDocument doc)
        {
            List<int> result = new List<int>();

            HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//table[@class='searchresultpaging'][1]/tr/td[2]/a");
            HtmlNode hn = doc.DocumentNode.SelectSingleNode("//table[@class='searchresultpaging'][1]/tr/td[2]/a/p/b");
            int currentPageNumber = Convert.ToInt32(hn.InnerText);
            bool flag = false;
            foreach (int pageNumber in hnc.Select(n => Convert.ToInt32(n.InnerText)))
            {
                if (pageNumber == currentPageNumber)
                    flag = true;
                if (pageNumber != currentPageNumber && !_pageNumbers.Contains(pageNumber) && flag)
                {
                    lock (_pageNumbers)
                    {
                        _pageNumbers.Add(pageNumber);
                        result.Add(pageNumber);
                    }
                }
            }

            return result;
        }

        private static List<string> GetProrertyUrls(HtmlDocument doc)
        {
            List<string> result = new List<string>();

            HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//table[@id='searchtable']/tr[@class='pRow ' or @class='pRow even']");
            if (hnc != null)
            {
                foreach (HtmlNode row in hnc)
                {
                    HtmlNode atag = row.SelectSingleNode("td/a[1]");
                    result.Add(atag.Attributes["href"].Value);
                }
            }

            hnc = doc.DocumentNode.SelectNodes("//table[@id='searchtable']/tr[@class='pRow enhanced']");
            if (hnc != null)
            {
                foreach (HtmlNode row in hnc)
                {
                    HtmlNode atag = row.SelectSingleNode("td[1]/table[@class='searchResultTable']/tr/td[@class='value']/div[@class='title']/a");
                    result.Add(atag.Attributes["href"].Value);
                }
            }

            return result;
        }

        private static BoligaProperty GetPropertyData(string url)
        {
            BoligaProperty result = new BoligaProperty
            {
                SogeresultaterGuid = _searchGuid
                ,
                Link = $"http://{_siteUrl}{url}"
            };

            using (var client = new HttpClient())
            {
                var uri = new Uri($"http://{_siteUrl}/{url}");

                var response = client.GetAsync(uri).Result;
                var responseString = response.Content.ReadAsStringAsync().Result;

                HtmlDocument htmlDoc = new HtmlDocument { OptionFixNestedTags = true };
                htmlDoc.LoadHtml(responseString);

                HtmlNode titelNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//div[@class='main-content paddingT']/h2[1]");
                string titelStr =
                    titelNode?.InnerText
                        .Trim();
                string titel = titelNode != null ? WebUtility.HtmlDecode(titelStr) : null;

                HtmlNode postnrNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//div[@class='main-content paddingT']/h3[1]");
                string postnrStr = null;
                if (postnrNode?.InnerText.Split(' ')[0].Length > 1)
                {
                    postnrStr = postnrNode?.InnerText
                        .Split(' ')[0]
                        .Trim();
                }
                string postnr = postnrNode != null ? WebUtility.HtmlDecode(postnrStr) : null;

                HtmlNode postnrTitelNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//div[@class='main-content paddingT']/h3[1]");
                string postnrTitelStr = null;
                if (postnrTitelNode?.InnerText.Split(' ')[1].Length > 1)
                {
                    postnrTitelStr = postnrTitelNode?.InnerText
                        .Split(' ')[1]
                        .Trim();
                }
                string postnrTitel = postnrNode != null ? WebUtility.HtmlDecode(postnrTitelStr) : null;

                HtmlNode kontantprisNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//table[@class='table estate-table']/tr[1]/td[1]/strong");
                string kontantprisSrt = null;
                if (kontantprisNode?.InnerText.Split(' ').Length > 1)
                {
                    kontantprisSrt = kontantprisNode?.InnerText.Split(' ')[0];
                }
                decimal? kontantpris = kontantprisSrt != null ? decimal.Parse(kontantprisSrt, _danishCulture) : (decimal?)null;

                HtmlNode ejerudgiftNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//*[text()[contains(.,'Ejerudgift')]]");
                string ejerudgiftStr = null;
                if (ejerudgiftNode?.InnerHtml.Split(BrRn, StringSplitOptions.None).Length > 1
                    && ejerudgiftNode?.InnerHtml
                    .Split(BrRn, StringSplitOptions.None)[1]
                    .Split(Small, StringSplitOptions.None).Length > 1)
                {
                    ejerudgiftStr =
                    ejerudgiftNode?.InnerHtml
                        .Split(BrRn, StringSplitOptions.None)[1]
                        .Split(Small, StringSplitOptions.None)[0]
                        .Trim();
                }
                decimal? ejerudgift = ejerudgiftStr != null ? decimal.Parse(ejerudgiftStr, _danishCulture) : (decimal?)null;

                HtmlNode kvmprisNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//table[@class='table estate-table']/tr/td[.='Kvmpris']/following-sibling::td");
                string kvmprisStr = null;
                if (kvmprisNode?.InnerText.Split(Small, StringSplitOptions.None).Length > 1
                    && kvmprisNode?.InnerText.Split(Small, StringSplitOptions.None)[0]
                    .Split(Krm, StringSplitOptions.None).Length > 1)
                {
                    kvmprisStr = kvmprisNode?.InnerText
                    .Split(Small, StringSplitOptions.None)[0]
                    .Split(Krm, StringSplitOptions.None)[0]
                    .Trim();
                }
                decimal? kvmpris = kvmprisStr != null ? decimal.Parse(kvmprisStr, _danishCulture) : (decimal?)null;

                HtmlNode typeNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//table[@class='table estate-table']/tr/td[.='Type']/following-sibling::td");
                string type = typeNode != null ? WebUtility.HtmlDecode(typeNode.InnerText) : null;

                HtmlNode boligNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//table[@class='table estate-table']/tr/td[.='Bolig']/following-sibling::td");
                string boligStr = boligNode?.InnerText
                    .Split(Msup2, StringSplitOptions.None)[0]
                    .Trim();
                boligStr = boligStr != null ? Regex.Replace(boligStr, @"[^\d]", "") : null;
                int? bolig = boligStr != null ? int.Parse(boligStr, NumberStyles.Number, _danishCulture) : (int?)null;

                HtmlNode grundNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//table[@class='table estate-table']/tr/td[.='Grund']/following-sibling::td");
                string grundStr = grundNode?.InnerText
                    .Split(Msup2, StringSplitOptions.None)[0]
                    .Trim();
                grundStr = grundStr != null ? Regex.Replace(grundStr, @"[^\d]", "") : null;
                int? grund = grundStr != null ? int.Parse(grundStr, NumberStyles.Number, _danishCulture) : (int?)null;

                HtmlNode vaerelserNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//table[@class='table estate-table']/tr/td[.='Værelser']/following-sibling::td");
                string vaerelserStr = vaerelserNode?.InnerText
                    .Trim();
                vaerelserStr = vaerelserStr != null ? Regex.Replace(vaerelserStr, @"[^\d]", "") : null;
                int? vaerelser = vaerelserStr != null ? int.Parse(vaerelserStr, NumberStyles.Number, _danishCulture) : (int?)null;

                HtmlNode etageNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//table[@class='table estate-table']/tr/td[.='Etage']/following-sibling::td");
                string etage = etageNode != null ? WebUtility.HtmlDecode(etageNode.InnerText.Trim()) : null;

                HtmlNode byggearNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//table[@class='table estate-table']/tr/td[.='Byggeår']/following-sibling::td");
                string byggearStr = byggearNode?.InnerText
                    .Trim();
                byggearStr = byggearStr != null ? Regex.Replace(byggearStr, @"[^\d]", "") : null;
                int? byggear = byggearStr != null ? int.Parse(byggearStr, NumberStyles.Number, _danishCulture) : (int?)null;

                HtmlNode oprettetNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//table[@class='table estate-table']/tr/td[.='Oprettet']/following-sibling::td");
                string oprettetStr = oprettetNode?.InnerText
                    .Trim();
                DateTime? oprettet = oprettetStr != null ? DateTime.ParseExact(oprettetStr, "dd-MM-yyyy", CultureInfo.InvariantCulture) : (DateTime?)null;

                HtmlNode liggetidNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//table[@class='table estate-table']/tr/td[.='Liggetid']/following-sibling::td");
                string liggetidStr = liggetidNode?.InnerText
                    .Split(' ')[0];
                liggetidStr = liggetidStr != null ? Regex.Replace(liggetidStr, @"[^\d]", "") : null;
                int? liggetid = liggetidStr != null ? int.Parse(liggetidStr, NumberStyles.Number, _danishCulture) : (int?)null;

                HtmlNode brokerLinkNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//a[@class='but brokerLink']");
                string brokerLink = brokerLinkNode?.Attributes["href"].Value
                    .Trim();

                HtmlNode butikTitelNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//div[@class='estate-50 margin-top margin-right agent-box fill']/div[@class='wrapper']/strong");
                string butikTitel = butikTitelNode != null ? WebUtility.HtmlDecode(butikTitelNode.InnerText.Trim()) : null;

                HtmlNode butikAdresseNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//div[@class='estate-50 margin-top margin-right agent-box fill']/div[@class='wrapper']");
                string butikAdresseStr = null;
                if (butikAdresseNode?.InnerHtml.Split(StrongBrStr, StringSplitOptions.None).Length > 1
                    && butikAdresseNode?.InnerHtml.Split(BrStr, StringSplitOptions.None).Length > 1)
                {
                    butikAdresseStr = butikAdresseNode?.InnerHtml
                        .Split(StrongBrStr, StringSplitOptions.None)[1]
                        .Split(BrStr, StringSplitOptions.None)[0]
                        .Trim();
                }
                string butikAdresse = butikAdresseStr != null ? WebUtility.HtmlDecode(butikAdresseStr.Trim()) : null;

                HtmlNode butikPostnrNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//div[@class='estate-50 margin-top margin-right agent-box fill']/div[@class='wrapper']");
                string butikPostnrStr = null;
                if (butikPostnrNode?.InnerHtml.Split(StrongBrStr, StringSplitOptions.None).Length > 1
                    && butikPostnrNode?.InnerHtml.Split(StrongBrStr, StringSplitOptions.None)[1]
                    .Split(BrStr, StringSplitOptions.None).Length > 1)
                {
                    butikPostnrStr = butikPostnrNode?.InnerHtml
                    .Replace("\r\n", "")
                    .Trim()
                    .Split(StrongBrStr, StringSplitOptions.None)[1]
                    .Split(BrStr, StringSplitOptions.None)[1]
                    .Trim()
                    .Split(' ')[0]
                    .Trim();
                }
                string butikPostnr = butikPostnrStr;

                HtmlNode butikPostnrTitelNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//div[@class='estate-50 margin-top margin-right agent-box fill']/div[@class='wrapper']");
                string butikPostnrTitelStr = null;
                if (butikPostnrTitelNode?.InnerHtml.Replace("\r\n", "").Trim().Split(StrongBrStr, StringSplitOptions.None).Length > 1
                    && butikPostnrTitelNode?.InnerHtml.Replace("\r\n", "").Trim().Split(StrongBrStr, StringSplitOptions.None)[1]
                    .Split(BrStr, StringSplitOptions.None).Length > 1
                    && butikPostnrTitelNode?.InnerHtml.Replace("\r\n", "").Trim().Split(StrongBrStr, StringSplitOptions.None)[1]
                    .Split(BrStr, StringSplitOptions.None)[1].Trim().Split(' ').Length > 1)
                {
                    butikPostnrTitelStr = butikPostnrTitelNode?.InnerHtml
                    .Replace("\r\n", "")
                    .Trim()
                    .Split(StrongBrStr, StringSplitOptions.None)[1]
                    .Split(BrStr, StringSplitOptions.None)[1]
                    .Trim()
                    .Split(' ')[1]
                    .Trim();
                }
                string butikPostnrTitel = WebUtility.HtmlDecode(butikPostnrTitelStr);

                HtmlNode prisforskelProcentdelNode =
                    htmlDoc.DocumentNode.SelectSingleNode(
                        "//div[@class='estate-50 margin-top gauge-box fill']/div[@class='wrapper']/strong");
                int? prisforskelProcentdel = null;
                if (prisforskelProcentdelNode?.InnerText.Split(ProcentStr, StringSplitOptions.None).Length > 1
                    && prisforskelProcentdelNode?.InnerText.Split(ProcentStr, StringSplitOptions.None).Length > 1)
                {
                    string prisforskelProcentdelNumber = prisforskelProcentdelNode.InnerText
                    .Split(ProcentStr, StringSplitOptions.None)[0];
                    string prisforskelProcentdelPlusMinus = prisforskelProcentdelNode.InnerText
                        .Split(ProcentStr, StringSplitOptions.None)[1];
                    prisforskelProcentdelNumber = prisforskelProcentdelPlusMinus == "lavere" ? $"-{prisforskelProcentdelNumber}" : prisforskelProcentdelNumber;
                    prisforskelProcentdel = prisforskelProcentdelNumber != null ? int.Parse(prisforskelProcentdelNumber, NumberStyles.Number, _danishCulture) : (int?)null;
                }

                HtmlNode kvmprisBoligenNode =
                    htmlDoc.DocumentNode.SelectSingleNode("//table[@class='table table-compare']/tr/td[contains(.,'Kvmpris boligen')]/following-sibling::td/strong");
                string kvmprisBoligenStr = kvmprisBoligenNode?.InnerText;
                decimal? kvmprisBoligen = kvmprisBoligenStr != null ? decimal.Parse(kvmprisBoligenStr, _danishCulture) : (decimal?)null;

                HtmlNode kvmprisOmradetNode =
                    htmlDoc.DocumentNode.SelectSingleNode("//table[@class='table table-compare']/tr/td[contains(.,'Kvmpris området')]/following-sibling::td/strong");
                string kvmprisOmradetNodeStr = kvmprisOmradetNode?.InnerText;
                decimal? kvmprisOmradet = kvmprisOmradetNodeStr != null ? decimal.Parse(kvmprisOmradetNodeStr, _danishCulture) : (decimal?)null;

                result.Titel = titel;
                result.Postnr = postnr;
                result.PostnrTitel = postnrTitel;
                result.Kontantpris = kontantpris;
                result.Ejerudgift = ejerudgift;
                result.Kvmpris = kvmpris;
                result.Type = type;
                result.Bolig = bolig;
                result.Grund = grund;
                result.Vaerelser = vaerelser;
                result.Etage = etage;
                result.Byggear = byggear;
                result.Oprettet = oprettet;
                result.Liggetid = liggetid;
                result.BrokerLink = brokerLink;
                result.ButikTitel = butikTitel;
                result.ButikAdresse = butikAdresse;
                result.ButikPostnr = butikPostnr;
                result.ButikPostnrTitel = butikPostnrTitel;
                result.PrisforskelProcentdel = prisforskelProcentdel;
                result.KvmprisBoligen = kvmprisBoligen;
                result.KvmprisOmradet = kvmprisOmradet;
            }

            return result;
        }

        private static async Task<bool> GetSearchPage(int pageNumber)
        {
            using (var client = new HttpClient())
            {
                var uri = new Uri($"{_startUrl}?page={pageNumber}");

                var response = await client.GetAsync(uri);
                var responseString = await response.Content.ReadAsStringAsync();

                HtmlDocument htmlDoc = new HtmlDocument { OptionFixNestedTags = true };
                htmlDoc.LoadHtml(responseString);

                List<int> newPageNumbers = GetNewSearchPageNumbers(htmlDoc);
                if (newPageNumbers == null)
                    return true;
                else
                {
                    ConcurrentDictionary<string, BoligaProperty> properties = new ConcurrentDictionary<string, BoligaProperty>();
                    Parallel.ForEach(GetProrertyUrls(htmlDoc), url =>
                    {
                        properties.TryAdd(url, GetPropertyData(url));
                    });

                    using (TransactionScope scope = new TransactionScope())
                    {
                        BoligaDBEntities context = null;
                        try
                        {
                            context = new BoligaDBEntities();
                            context.Configuration.AutoDetectChangesEnabled = false;

                            int count = 0;
                            foreach (var p in properties)
                            {
                                ++count;
                                context = AddToContext(context, p.Value, count, 100, true);
                                _counter += 1;
                                _pbar.Tick("Properties processed " + _counter);
                            }

                            context.SaveChanges();
                        }
                        finally
                        {
                            context?.Dispose();
                        }

                        scope.Complete();
                    }

                    properties.Clear();

                    foreach (int pn in newPageNumbers)
                    {
                        await GetSearchPage(pn);
                    }
                }
            }

            return true;
        }

        private static BoligaDBEntities AddToContext(BoligaDBEntities context, BoligaProperty entity, int count, int commitCount, bool recreateContext)
        {
            context.Set<BoligaProperty>().Add(entity);

            if (count % commitCount == 0)
            {
                context.SaveChanges();
                if (recreateContext)
                {
                    context.Dispose();
                    context = new BoligaDBEntities();
                    context.Configuration.AutoDetectChangesEnabled = false;
                }
            }

            return context;
        }

        static void Main()
        {
            try
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Green;
                DateTime startDateTime = DateTime.Now;
                Console.WriteLine($"Boliga crawler started at - {startDateTime}");
                Console.WriteLine();

                _startUrl = ConfigurationManager.AppSettings["startUrl"];
                _siteUrl = new Uri(_startUrl).Host;
                _searchGuid = new Guid(_startUrl.Split('/').Last());
                _pageNumbers = new List<int>();
                _counter = 0;
                _totalPropertiesCount = 0;
                _danishCulture = new CultureInfo("da-DK");

                List<Task> taskList = new List<Task>();

                _totalPropertiesCount = GetTotalPropertiesCount();
                int totalPagesCount = _totalPropertiesCount / 20 + 1;

                _pbar = new ProgressBar(_totalPropertiesCount, "Starting...", ConsoleColor.Cyan, '\u2593');

                int pagesPerTask = totalPagesCount / 3;

                for (int i = 0; i < 3; i++)
                {
                    int idx = i;
                    int startPageNumber = idx * pagesPerTask + 1;
                    taskList.Add(GetSearchPage(startPageNumber));
                    _pageNumbers.Add(startPageNumber);
                }

                Task.WaitAll(taskList.ToArray());

                _pbar.Dispose();
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Boliga crawler started at - {startDateTime}");
                Console.WriteLine($"Boliga crawler stopped at - {DateTime.Now}");
                Console.WriteLine("Press enter key...");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                _pbar?.Dispose();
                Console.SetCursorPosition(0, 6);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("Press any key...");
                Console.ReadLine();
            }
        }
    }
}

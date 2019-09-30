using System;
using AngleSharp;
using AngleSharp.Dom;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RidingParser
{
    class RidingParser
    {
        static readonly HttpClient client = new HttpClient();

        static void Main(string[] args)
        {
            Angly().Wait();
        }

        static async Task PopulateCandidate(Candidate candidate){
            if(candidate.getUrl() == null || candidate.getUrl().Trim() == ""){
                return;
            }
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync("https://en.wikipedia.org/" + candidate.getUrl()) ;

            var infobox = document.QuerySelector(".infobox");
            if(infobox == null){
                return;
            }
            
            var rows = infobox.QuerySelectorAll("tr");
            foreach(var row in rows){
                var keyElem = row.QuerySelector("th");
                var valueElem = row.QuerySelector("td");
                if(keyElem != null && valueElem != null){
                    String key = keyElem.TextContent.Trim();
                    String value = valueElem.TextContent.Trim();
                    if(key == "Born"){
                        var bday = valueElem.QuerySelector(".bday");
                        if(bday != null){
                            candidate.setBorn(bday.TextContent.Trim());
                        }
                        else{
                            candidate.setBorn(valueElem.TextContent.Trim());
                        }
                    }
                    if(key == "Profession" || key == "Occupation"){
                        candidate.setProfession(value);
                    }
                }
            }

        }

        static async Task Angly(){
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync("https://en.wikipedia.org/wiki/Results_of_the_2015_Canadian_federal_election_by_riding");

            var tableSelector = document.QuerySelectorAll("table");

            List<Candidate> candidates = new List<Candidate>();


            bool first = true;

            foreach (var table in tableSelector)
            {
                IElement prev = table.PreviousElementSibling;
                while(prev.NodeName != "H2"){
                    prev = prev.PreviousElementSibling;
                }
                String province = prev.TextContent.Replace("[edit]", "");

                IElement prevLocale = table.PreviousElementSibling;
                while(prevLocale.NodeName != "H3" && prevLocale.NodeName != "H2"){
                    prevLocale = prevLocale.PreviousElementSibling;
                }
                String locale = "";
                if(prevLocale.NodeName == "H3"){
                    locale = prevLocale.TextContent.Replace("[edit]", "");
                }
                
                IHtmlCollection<IElement> rows = table.QuerySelectorAll("tr");
                bool firstRow = true;
                bool secondRow = false;
                List<String> headers = new List<String>();

                foreach (var row in rows){
                    if(firstRow){
                        firstRow = false;
                        secondRow = true;
                    }
                    else if(secondRow){
                        IHtmlCollection<IElement> headerElems = row.QuerySelectorAll("th");
                        secondRow = false;
                        foreach (var header in headerElems)
                        {
                            headers.Add(header.TextContent.Trim());
                        }
                    }
                    else{
                        IHtmlCollection<IElement> dataElems = row.QuerySelectorAll("td");
                        int column = 0;
                        String riding = "";
                        bool wasElected = false;
                        foreach (var value in dataElems)
                        {
                            if(column == 0){
                                riding = value.TextContent.Trim();
                            }
                            else if(column < headers.Count){
                                if(wasElected && headers[column - 1] != "" && value.TextContent.Trim() != ""){
                                    wasElected = false;
                                    String url = "";
                                    IElement link = value.QuerySelector("a");
                                    if(link != null){
                                        if(!(link.TextContent.StartsWith("[") && link.TextContent.EndsWith("]"))){
                                            url = link.Attributes["href"].Value;
                                        }
                                    }

                                    Candidate candidate = new Candidate();
                                    candidate.setName(Regex.Replace(value.TextContent.Trim(), @"([^\w -]|\%|\*|\d)|(\(?nomination meeting.*\)?)", "", RegexOptions.None));
                                    candidate.setParty(headers[column - 1]);
                                    candidate.setRiding(riding);
                                    candidate.setProvince(province);
                                    candidate.setLocale(locale);
                                    candidate.setUrl(url);
                                    candidates.Add(candidate);
                                    PopulateCandidate(candidate).Wait();
                                    if(first){
                                        first = false;
                                        Console.WriteLine(candidate.header());
                                    }
                                    Console.WriteLine(candidate.toCSV());
                                }
                                else{
                                    if(value.GetAttribute("bgcolor") != null){
                                        wasElected = true;
                                    }
                                }
                            }
                            column++;
                        }
                    }
                }
            }
        }
    }
}


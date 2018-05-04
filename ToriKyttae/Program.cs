using System;
using ToriKyttae;

namespace ToriKyttae
{
    class Program
    {
        static void Main(string[] args)
        {
            string url = "";
            ToriQuery query = new ToriQuery();
            query.Parameters.Add(new KeywordParameter("midi"));
            query.Parameters.Add(new RegionParameter(Regions.KokoSuomi));
            ToriClassified[] classifieds = query.GetAllClassifieds();

            Console.WriteLine(url);
        }
    }
}

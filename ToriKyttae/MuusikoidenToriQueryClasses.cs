

using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Web;

namespace ToriKyttae
{
    #region QueryStuff

    class ToriQuery
    {
        public List<ToriQueryParameter> Parameters;
        public string BaseUrl;

        public ToriQuery(string Hostname = "https://muusikoiden.net", string ToriUrl = "/tori/haku.php")
        {
            this.BaseUrl = Hostname + ToriUrl;
            Parameters = new List<ToriQueryParameter>();
        }

        string paramstr = "";

        public string CreateUrl()
        {
            string result = "";

            result = BaseUrl;
            if (!result.EndsWith("?"))
            {
                result += "?";
            }

            ToriQueryParameter[] NonAreals = NonArealParameters();
            if (NonAreals != null)
            {
                foreach (ToriQueryParameter param in NonAreals)
                {
                    if (!param.Value().Equals(""))
                    {
                        if (!paramstr.Equals(""))
                        {
                            paramstr += "&";
                        }

                        paramstr += param.Key() + "=" + HttpUtility.UrlEncode(param.Value());
                    }
                }
            }

            ToriQueryParameter Areal = GetAreaParameter();
            if (Areal != null)
            {
                if (!Areal.Value().Equals(""))
                {
                    if (!paramstr.Equals(""))
                    {
                        paramstr += "&";
                    }

                    paramstr += Areal.Key() + "=" + HttpUtility.UrlEncode(Areal.Value());
                }
            }

            return result + paramstr;
        }

        /// <summary>
        /// Returns a parameter that is the best definition for the area this query targets at. Returns MunicipalityParameter and if there isn't any, returns a ProvinceParameter and if there isn't any, returns a RegionParameter and if there isn't any, returns null.
        /// </summary>
        /// <returns></returns>
        public ToriQueryParameter GetAreaParameter()
        {
            ToriQueryParameter result = null;

            foreach (ToriQueryParameter param in Parameters)
            {
                if (param is MunicipalityParameter)
                {
                    result = param;
                    break;
                }
            }

            if (result == null)
            {
                foreach (ToriQueryParameter param in Parameters)
                {
                    if (param is ProvinceParameter)
                    {
                        result = param;
                        break;
                    }
                }
            }

            if (result == null)
            {
                foreach (ToriQueryParameter param in Parameters)
                {
                    if (param is RegionParameter)
                    {
                        result = param;
                        break;
                    }
                }
            }

            return result;
        }

        private ToriQueryParameter[] NonArealParameters()
        {
            List<ToriQueryParameter> result = new List<ToriQueryParameter>();

            foreach (ToriQueryParameter param in Parameters)
            {
                if (!(param is RegionParameter) && !(param is ProvinceParameter) && !(param is MunicipalityParameter))
                {
                    result.Add(param);
                }
            }

            if (result.Count > 0)
            {
                return result.ToArray();
            }
            else
            {
                return null;
            }
        }

        public OffsetParameter OffsetParameter
        {
            get
            {
                foreach (ToriQueryParameter param in Parameters)
                {
                    if (param is OffsetParameter)
                    {
                        return (OffsetParameter)param;
                    }
                }
                return null;
            }

            set
            {
                OffsetParameter current = OffsetParameter;
                if (current == null)
                {
                    Parameters.Add(value);
                }
                else
                {
                    Parameters.Remove(current);
                    Parameters.Add(current);
                }
            }
        }

        public ToriResponse GetResponse()
        {
            ToriResponse result = null;
            string url = CreateUrl();
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            HtmlDocument doc = new HtmlDocument();
            List<ToriClassified> classifieds = new List<ToriClassified>();
            Console.Write("Requesting " + url);
            doc.Load(response.GetResponseStream(), System.Text.Encoding.GetEncoding("ISO-8859-1"));
            response.Close();
            response.Dispose();
            Console.Write(" - done.\n");
            // etitään "HAKUTULOKSET"-elementti
            HtmlNode tulosotsikkoNode = doc.DocumentNode.SelectSingleNode("//*[.='Hakutulokset']");
            if (tulosotsikkoNode != null)
            {

                int queried = -1;
                int count = -1;

                if (int.TryParse(tulosotsikkoNode.NextSibling.NextSibling.InnerText, out queried) && int.TryParse(tulosotsikkoNode.NextSibling.NextSibling.NextSibling.NextSibling.InnerText, out count))
                {
                    // tää on eka sivu
                }

                result = new ToriResponse(queried, count);

                // tsekataan onks paginointia ja jos on ni otetaan talteen "seuraava"-linkin urli
                HtmlNodeCollection seuraavaLinks = doc.DocumentNode.SelectNodes(@"//a[contains(@href,'offset') and text() = 'seuraava']");
                if (seuraavaLinks != null)
                {
                    result.NextPageUrl = seuraavaLinks[0].GetAttributeValue("href", "");
                }


                HtmlNodeCollection tablenodes = tulosotsikkoNode.ParentNode.SelectNodes("table");
                // eka table on hakulomake, toka table on tulostiiviste ja loput on hakutuloksia

                if (tablenodes != null)
                {
                    int counter = 0;
                    foreach (HtmlNode node in tablenodes)
                    {
                        if (counter >= 2 && counter != tablenodes.Count - 1)
                        { 
                            ToriClassified classified = ToriClassified.FromHtmlNode(node);
                            if (classified != null)
                            {
                                classifieds.Add(classified);
                            }
                        }

                        counter++;
                    }
                }

                // jaa jaa eli kyseessä on ehkä offsetattu sivu


            }

            if (classifieds.Count > 0)
            {
                result.Classifieds = classifieds.ToArray();
            }

            return result;
        }

        public ToriClassified[] GetAllClassifieds()
        {
            List<ToriClassified> Result = new List<ToriClassified>();

            ToriQuery query = this;

            bool GotUngatheredResults = true;
            while (GotUngatheredResults)
            {
                ToriResponse response = query.GetResponse();

                if (response == null)
                {
                    GotUngatheredResults = false;
                }
                else
                {
                    if (response.Classifieds != null)
                    {
                        Result.AddRange(response.Classifieds);
                    }

                    if (response.NextPageUrl != null)
                    {
                        query = Operations.ToriQueryFromToriUrl(response.NextPageUrl);
                    }
                    else
                    {
                        GotUngatheredResults = false;
                    }
                }
            }

            if (Result.Count > 0)
            {
                return Result.ToArray();
            }
            else
            {
                return null;
            }
        }

    }

    class ToriClassified
    {
        private string _user;
        private string _userProfileUrl;
        private string _userPhone;
        private string _userEmail;

        private string _title;
        private string _body;
        private int _price;

        private string _catpath;
        private string _location;
        private string _url;

        private DateTime _added;
        private DateTime _expiresAt;

        private ToriImage[] _images;

        private ClassifiedTypes _type;

        internal ToriClassified(string User, string UserProfileUrl, string Title, string Body, int Price, ToriImage[] Images)
        {
            this.User = User;
            _userProfileUrl = UserProfileUrl;
            this.Title = Title;
            this.Body = Body;
            this.Price = Price;
            this.Images = Images;
        }

        public static ToriClassified FromHtmlNode(HtmlNode node)
        {
            ToriClassified result = null;
            if (node.ChildNodes.Count == 5)
            {
                if (node.FirstChild.HasClass("bg2") && node.LastChild.HasClass("bg2"))
                {
                    if (node.FirstChild.FirstChild.HasClass("tori_title"))
                    {
                        result = new ToriClassified("", "", "", "", -1, null);
                        string typetag = node.FirstChild.FirstChild.FirstChild.InnerText.Replace(":", "");
                        // otetaan ekasta lapsesta title ja url
                        switch (typetag)
                        {
                            case "Myydään":
                                result.Type = ClassifiedTypes.Myydaan;
                                break;
                            case "Ostetaan":
                                result.Type = ClassifiedTypes.Ostetaan;
                                break;
                            case "Vuokrataan":
                                result.Type = ClassifiedTypes.Vuokrataan;
                                break;
                            case "Vaihdetaan":
                                result.Type = ClassifiedTypes.Vaihdetaan;
                                break;
                            case "Halutaan vuokrata":
                                result.Type = ClassifiedTypes.HalutaanVuokrata;
                                break;
                            case "Muu":
                                result.Type = ClassifiedTypes.Muut;
                                break;
                        }

                        string title = node.FirstChild.FirstChild.InnerText.Replace(typetag + ": ", "").Split("&nbsp;", StringSplitOptions.RemoveEmptyEntries)[0];


                        HtmlNode headerlink = node.FirstChild.FirstChild.SelectSingleNode("a");
                        string urli = headerlink.GetAttributeValue("href", "EIOOURLIAKAKKARIPULI");
                        if (!urli.Equals("EIOOURLIAKAKKARIPULI"))
                        {
                            result.Title = title;
                            result.Url = urli;
                        }

                        if (node.FirstChild.LastChild.ChildNodes.Count == 1)
                        {
                            if (node.FirstChild.LastChild.FirstChild.Name.ToLower().Equals("small"))
                            {
                                HtmlNode datenode = node.FirstChild.LastChild.FirstChild.SelectSingleNode("span");
                                string addedString = datenode.GetAttributeValue("title", "").Replace("Lisätty: ", "");
                                if (addedString.Contains("\n"))
                                {
                                    addedString = addedString.Split("\n", StringSplitOptions.RemoveEmptyEntries)[0];
                                }

                                string dateformat = "dd.MM.yyyy HH:mm";
                                result.Added = DateTime.ParseExact(addedString, dateformat, null);

                                HtmlNode expiringdatenode = node.FirstChild.LastChild.FirstChild.ChildNodes[3];
                                string expiringString = expiringdatenode.InnerText.Replace("&nbsp;", "").Replace("Voimassa:", "");

                                result.ExpiresAt = DateTime.ParseExact(expiringString, "dd.MM.yyyy", null);

                            }
                        }

                        HtmlNode usernode = node.ChildNodes[1].FirstChild.FirstChild.FirstChild.FirstChild.SelectSingleNode("a");
                        if (usernode != null)
                        {
                            result.User = usernode.InnerText;
                            result.UserProfileUrl = usernode.GetAttributeValue("href", "");
                        }

                        HtmlNode bodynode = node.ChildNodes[2].FirstChild;
                        string bodytext = "";
                        string pricestr = "";

                        foreach (HtmlNode childnode in node.ChildNodes[2].FirstChild.ChildNodes)
                        {
                            if (childnode.Name.ToLower().Equals("b") && childnode.InnerText.Equals("Hinta:"))
                            {
                                pricestr = childnode.NextSibling.InnerText.Replace("&euro;", "");
                                break;
                            }

                            bodytext += childnode.InnerText;
                        }

                        result.Body = bodytext;
                        int price = 0;

                        if (int.TryParse(pricestr, out price))
                        {
                            result.Price = price;
                        }

                    }
                }
            }
            return result;
        }

        public string User { get => _user; set => _user = value; }
        public string UserProfileUrl { get => _userProfileUrl; set => _userProfileUrl = value; }
        public string Title { get => _title; set => _title = value; }
        public string Body { get => _body; set => _body = value; }
        public int Price { get => _price; set => _price = value; }
        internal ToriImage[] Images { get => _images; set => _images = value; }

        public string CategoryPath { get => _catpath; set => _catpath = value; }
        public string Location { get => _location; set => _location = value; }
        public string Url { get => _url; set => _url = value; }

        public string UserPhone { get => _userPhone; set => _userPhone = value; }
        public string UserEmail { get => _userEmail; set => _userEmail = value; }
        public DateTime Added { get => _added; set => _added = value; }
        public DateTime ExpiresAt { get => _expiresAt; set => _expiresAt = value; }
        internal ClassifiedTypes Type { get => _type; set => _type = value; }
    }

    class ToriImage
    {
        private string _thumbUrl;
        private string _url;

        internal ToriImage(string ThumbUrl, string Url)
        {
            this.ThumbUrl = ThumbUrl;
            this.Url = Url;
        }

        public string ThumbUrl { get => _thumbUrl; set => _thumbUrl = value; }
        public string Url { get => _url; set => _url = value; }
    }

    /// <summary>
    /// Enumeration representing possible values for "Mistä haetaan"-parameter (="Where to search").
    /// </summary>
    enum SearchIncludeValue
    {
        /// <summary>
        /// (="Koko ilmoituksista")
        /// </summary>
        FromWholeClassifieds,

        /// <summary>
        /// (="Vain otsikoista")
        /// </summary>
        OnlyFromTitles
    }

    interface ToriQueryParameter
    {
        string Key();
        string Value();
    }

    class KeywordParameter : ToriQueryParameter
    {
        public string Keyword;

        public KeywordParameter(string Keyword)
        {
            this.Keyword = Keyword;
        }

        public string Key()
        {
            return "keyword";
        }

        public string Value()
        {
            return Keyword;
        }
    }

    class RegionParameter : ToriQueryParameter
    {
        public Regions Region;

        public RegionParameter(Regions Region)
        {
            this.Region = Region;
        }

        public string Key()
        {
            return "location";
        }

        public string Value()
        {
            return Operations.RegionEnumToString(Region);
        }
    }

    class ProvinceParameter : ToriQueryParameter
    {
        public Provinces Province;

        public ProvinceParameter(Provinces Province)
        {
            this.Province = Province;
        }

        public string Key()
        {
            return "province";
        }

        public string Value()
        {
            return Operations.ProvinceEnumToString(Province);
        }
    }

    class MunicipalityParameter : ToriQueryParameter
    {
        public string Municipality;

        public MunicipalityParameter(string Municipality)
        {
            this.Municipality = Municipality;
        }

        public string Key()
        {
            return "city";
        }

        public string Value()
        {
            return Operations.MunicipalityNameToNumber(Municipality).ToString();
        }
    }

    class ClassifiedTypeParameter : ToriQueryParameter
    {
        public ClassifiedTypes Type;

        public ClassifiedTypeParameter(ClassifiedTypes Type)
        {
            this.Type = Type;
        }

        public string Key()
        {
            return "type";
        }

        public string Value()
        {
            return Operations.ClassifiedTypesToString(Type);
        }
    }

    class MinimumPriceParameter : ToriQueryParameter
    {
        public int MinimumPrice;

        public MinimumPriceParameter(int MinimumPrice)
        {
            this.MinimumPrice = MinimumPrice;
        }

        public string Key()
        {
            return "price_min";
        }

        public string Value()
        {
            return MinimumPrice.ToString();
        }
    }

    class MaximumPriceParameter : ToriQueryParameter
    {
        public int MaximumPrice;

        public MaximumPriceParameter(int MaximumPrice)
        {
            this.MaximumPrice = MaximumPrice;
        }

        public string Key()
        {
            return "price_max";
        }

        public string Value()
        {
            return MaximumPrice.ToString();
        }
    }

    class CategoryParameter : ToriQueryParameter
    {
        public int Category;

        public CategoryParameter(int Category)
        {
            this.Category = Category;
        }

        public string Key()
        {
            return "category";
        }

        public string Value()
        {
            throw new NotImplementedException();
        }
    }

    class OnlyWithImageParameter : ToriQueryParameter
    {
        public string Key()
        {
            return "with_image";
        }

        public string Value()
        {
            return "1";
        }
    }

    class SortingParameter : ToriQueryParameter
    {
        public Sortings Sorting;

        public SortingParameter(Sortings Sorting)
        {
            this.Sorting = Sorting;
        }

        public string Key()
        {
            return "sort";
        }

        public string Value()
        {
            return Operations.SortingsToString(Sorting);
        }
    }

    class OffsetParameter : ToriQueryParameter
    {
        public int Offset;

        public OffsetParameter(int Offset)
        {
            this.Offset = Offset;
        }

        public string Key()
        {
            return "offset";
        }

        public string Value()
        {
            return Offset.ToString();
        }
    }


    /// <summary>
    /// Enumeration representing possible values for "Lääni"-parameter (="Region")
    /// </summary>
    enum Regions
    {
        /// <summary>
        /// Whole Finland
        /// </summary>
        KokoSuomi,

        /// <summary>
        /// Everywhere else than Finland
        /// </summary>
        Other,

        /// <summary>
        /// Åland islands
        /// </summary>
        Ahvenanmaa,

        /// <summary>
        /// Southern Finland
        /// </summary>
        EtelaSuomenLaani,

        /// <summary>
        /// Eastern Finland
        /// </summary>
        ItaSuomenLaani,

        /// <summary>
        /// Lapland region
        /// </summary>
        LapinLaani,

        /// <summary>
        /// Western Finland
        /// </summary>
        LansiSuomenLaani,

        /// <summary>
        /// Oulu region
        /// </summary>
        OulunLaani
    }

    /// <summary>
    /// Enumeration representing possible values for "Maakunta"-parameter (="Province")
    /// </summary>
    enum Provinces
    {
        Ahvenanmaa,
        EtelaKarjala,
        EtelaPohjanmaa,
        EtelaSavo,
        ItaUusimaa,
        Kainuu,
        KantaHame,
        KeskiPohjanmaa,
        KeskiSuomi,
        Kymenlaakso,
        Lappi,
        Pirkanmaa,
        Pohjanmaa,
        PohjoisKarjala,
        PohjoisPohjanmaa,
        PohjoisSavo,
        PaijatHame,
        Satakunta,
        Uusimaa,
        VarsinaisSuomi
    }

    static class Operations
    {
        public static string ProvinceEnumToString(Provinces ProvinceEnum)
        {
            switch (ProvinceEnum)
            {
                case Provinces.Ahvenanmaa:
                    return "Ahvenanmaa";

                case Provinces.EtelaKarjala:
                    return "Etelä-Karjala";

                case Provinces.EtelaPohjanmaa:
                    return "Etelä-Pohjanmaa";

                case Provinces.EtelaSavo:
                    return "Etelä-Savo";

                case Provinces.ItaUusimaa:
                    return "Itä-Uusimaa";

                case Provinces.Kainuu:
                    return "Kainuu";

                case Provinces.KantaHame:
                    return "Kanta-Häme";

                case Provinces.KeskiPohjanmaa:
                    return "Keski-Pohjanmaa";

                case Provinces.KeskiSuomi:
                    return "Keski-Suomi";

                case Provinces.Kymenlaakso:
                    return "Kymenlaakso";

                case Provinces.Lappi:
                    return "Lappi";

                case Provinces.Pirkanmaa:
                    return "Pirkanmaa";

                case Provinces.Pohjanmaa:
                    return "Pohjanmaa";

                case Provinces.PohjoisKarjala:
                    return "Pohjois-Karjala";

                case Provinces.PohjoisPohjanmaa:
                    return "Pohjois-Pohjanmaa";

                case Provinces.PohjoisSavo:
                    return "Pohjois-Savo";

                case Provinces.PaijatHame:
                    return "Päijät-Häme";

                case Provinces.Satakunta:
                    return "Satakunta";

                case Provinces.Uusimaa:
                    return "Uusimaa";

                case Provinces.VarsinaisSuomi:
                    return "Varsinais-Suomi";

                default:
                    return "";
            }
        }

        public static Provinces StringToProvince(string str, Provinces defaultvalue = Provinces.Uusimaa)
        {
            foreach (Provinces province in Enum.GetValues(typeof(Provinces)))
            {
                if (str.Equals(ProvinceEnumToString(province)))
                {
                    return province;
                }
            }
            return defaultvalue;
        }

        public static string RegionEnumToString(Regions RegionEnum)
        {
            switch (RegionEnum)
            {
                case Regions.KokoSuomi:
                    return "suomi";
                case Regions.Other:
                    return "other";
                case Regions.Ahvenanmaa:
                    return "Ahvenanmaa";
                case Regions.EtelaSuomenLaani:
                    return "Etelä-Suomen lääni";
                case Regions.ItaSuomenLaani:
                    return "Itä-Suomen lääni";
                case Regions.LapinLaani:
                    return "Lapin lääni";
                case Regions.LansiSuomenLaani:
                    return "Länsi-Suomen lääni";
                case Regions.OulunLaani:
                    return "Oulun lääni";
                default:
                    return "";
            }
        }

        public static Regions StringToRegion(string str, Regions defaultvalue = Regions.KokoSuomi)
        {
            foreach (Regions region in Enum.GetValues(typeof(Regions)))
            {
                if (str.Equals(RegionEnumToString(region)))
                {
                    return region;
                }
            }
            return defaultvalue;
        }

        /// <summary>
        /// Converts a municipality name to a municipality number. Returns -1 if no number is found.
        /// </summary>
        /// <param name="MunicipalityName"></param>
        /// <returns></returns>
        public static int MunicipalityNameToNumber(string MunicipalityName)
        {
            string[] data = HttpUtility.HtmlDecode("5,Alaj\u00E4rvi\r\n9,Alavieska\r\n10,Alavus\r\n16,Asikkala\r\n18,Askola\r\n19,Aura\r\n20,Akaa\r\n35,Br\u00E4nd\u00F6\r\n43,Ecker\u00F6\r\n46,Enonkoski\r\n47,Enonteki\u00F6\r\n49,Espoo\r\n50,Eura\r\n51,Eurajoki\r\n52,Evij\u00E4rvi\r\n60,Finstr\u00F6m\r\n61,Forssa\r\n62,F\u00F6gl\u00F6\r\n65,Geta\r\n69,Haapaj\u00E4rvi\r\n71,Haapavesi\r\n72,Hailuoto\r\n74,Halsua\r\n75,Hamina\r\n76,Hammarland\r\n77,Hankasalmi\r\n78,Hanko\r\n79,Harjavalta\r\n81,Hartola\r\n82,Hattula\r\n86,Hausj\u00E4rvi\r\n90,Hein\u00E4vesi\r\n91,Helsinki\r\n92,Vantaa\r\n97,Hirvensalmi\r\n98,Hollola\r\n99,Honkajoki\r\n102,Huittinen\r\n103,Humppila\r\n105,Hyrynsalmi\r\n106,Hyvink\u00E4\u00E4\r\n108,H\u00E4meenkyr\u00F6\r\n109,H\u00E4meenlinna\r\n111,Heinola\r\n139,Ii\r\n140,Iisalmi\r\n142,Iitti\r\n143,Ikaalinen\r\n145,Ilmajoki\r\n146,Ilomantsi\r\n148,Inari\r\n149,Inkoo\r\n151,Isojoki\r\n152,Isokyr\u00F6\r\n153,Imatra\r\n165,Janakkala\r\n167,Joensuu\r\n169,Jokioinen\r\n170,Jomala\r\n171,Joroinen\r\n172,Joutsa\r\n176,Juuka\r\n177,Juupajoki\r\n178,Juva\r\n179,Jyv\u00E4skyl\u00E4\r\n181,J\u00E4mij\u00E4rvi\r\n182,J\u00E4ms\u00E4\r\n186,J\u00E4rvenp\u00E4\u00E4\r\n202,Kaarina\r\n204,Kaavi\r\n205,Kajaani\r\n208,Kalajoki\r\n211,Kangasala\r\n213,Kangasniemi\r\n214,Kankaanp\u00E4\u00E4\r\n216,Kannonkoski\r\n217,Kannus\r\n218,Karijoki\r\n224,Karkkila\r\n226,Karstula\r\n230,Karvia\r\n231,Kaskinen\r\n232,Kauhajoki\r\n233,Kauhava\r\n235,Kauniainen\r\n236,Kaustinen\r\n239,Keitele\r\n240,Kemi\r\n241,Keminmaa\r\n244,Kempele\r\n245,Kerava\r\n249,Keuruu\r\n250,Kihni\u00F6\r\n256,Kinnula\r\n257,Kirkkonummi\r\n260,Kitee\r\n261,Kittil\u00E4\r\n263,Kiuruvesi\r\n265,Kivij\u00E4rvi\r\n271,Kokem\u00E4ki\r\n272,Kokkola\r\n273,Kolari\r\n275,Konnevesi\r\n276,Kontiolahti\r\n280,Korsn\u00E4s\r\n284,Koski Tl\r\n285,Kotka\r\n286,Kouvola\r\n287,Kristiinankaupunki\r\n288,Kruunupyy\r\n290,Kuhmo\r\n291,Kuhmoinen\r\n295,Kumlinge\r\n297,Kuopio\r\n300,Kuortane\r\n301,Kurikka\r\n304,Kustavi\r\n305,Kuusamo\r\n309,Outokumpu\r\n312,Kyyj\u00E4rvi\r\n316,K\u00E4rk\u00F6l\u00E4\r\n317,K\u00E4rs\u00E4m\u00E4ki\r\n318,K\u00F6kar\r\n320,Kemij\u00E4rvi\r\n322,Kemi\u00F6nsaari\r\n398,Lahti\r\n399,Laihia\r\n400,Laitila\r\n402,Lapinlahti\r\n403,Lappaj\u00E4rvi\r\n405,Lappeenranta\r\n407,Lapinj\u00E4rvi\r\n408,Lapua\r\n410,Laukaa\r\n416,Lemi\r\n417,Lemland\r\n418,Lemp\u00E4\u00E4l\u00E4\r\n420,Lepp\u00E4virta\r\n421,Lestij\u00E4rvi\r\n422,Lieksa\r\n423,Lieto\r\n425,Liminka\r\n426,Liperi\r\n430,Loimaa\r\n433,Loppi\r\n434,Loviisa\r\n435,Luhanka\r\n436,Lumijoki\r\n438,Lumparland\r\n440,Luoto\r\n441,Luum\u00E4ki\r\n444,Lohja\r\n445,Parainen\r\n475,Maalahti\r\n478,Maarianhamina\r\n480,Marttila\r\n481,Masku\r\n483,Merij\u00E4rvi\r\n484,Merikarvia\r\n489,Miehikk\u00E4l\u00E4\r\n491,Mikkeli\r\n494,Muhos\r\n495,Multia\r\n498,Muonio\r\n499,Mustasaari\r\n500,Muurame\r\n503,Myn\u00E4m\u00E4ki\r\n504,Myrskyl\u00E4\r\n505,M\u00E4nts\u00E4l\u00E4\r\n507,M\u00E4ntyharju\r\n508,M\u00E4ntt\u00E4-Vilppula\r\n529,Naantali\r\n531,Nakkila\r\n535,Nivala\r\n536,Nokia\r\n538,Nousiainen\r\n541,Nurmes\r\n543,Nurmij\u00E4rvi\r\n545,N\u00E4rpi\u00F6\r\n560,Orimattila\r\n561,Orip\u00E4\u00E4\r\n562,Orivesi\r\n563,Oulainen\r\n564,Oulu\r\n576,Padasjoki\r\n577,Paimio\r\n578,Paltamo\r\n580,Parikkala\r\n581,Parkano\r\n583,Pelkosenniemi\r\n584,Perho\r\n588,Pertunmaa\r\n592,Pet\u00E4j\u00E4vesi\r\n593,Pieks\u00E4m\u00E4ki\r\n595,Pielavesi\r\n598,Pietarsaari\r\n599,Peders\u00F6ren kunta\r\n601,Pihtipudas\r\n604,Pirkkala\r\n607,Polvij\u00E4rvi\r\n608,Pomarkku\r\n609,Pori\r\n611,Pornainen\r\n614,Posio\r\n615,Pudasj\u00E4rvi\r\n616,Pukkila\r\n619,Punkalaidun\r\n620,Puolanka\r\n623,Puumala\r\n624,Pyht\u00E4\u00E4\r\n625,Pyh\u00E4joki\r\n626,Pyh\u00E4j\u00E4rvi\r\n630,Pyh\u00E4nt\u00E4\r\n631,Pyh\u00E4ranta\r\n635,P\u00E4lk\u00E4ne\r\n636,P\u00F6yty\u00E4\r\n638,Porvoo\r\n678,Raahe\r\n680,Raisio\r\n681,Rantasalmi\r\n683,Ranua\r\n684,Rauma\r\n686,Rautalampi\r\n687,Rautavaara\r\n689,Rautj\u00E4rvi\r\n691,Reisj\u00E4rvi\r\n694,Riihim\u00E4ki\r\n697,Ristij\u00E4rvi\r\n698,Rovaniemi\r\n700,Ruokolahti\r\n702,Ruovesi\r\n704,Rusko\r\n707,R\u00E4\u00E4kkyl\u00E4\r\n710,Raasepori\r\n729,Saarij\u00E4rvi\r\n732,Salla\r\n734,Salo\r\n736,Saltvik\r\n738,Sauvo\r\n739,Savitaipale\r\n740,Savonlinna\r\n742,Savukoski\r\n743,Sein\u00E4joki\r\n746,Sievi\r\n747,Siikainen\r\n748,Siikajoki\r\n749,Siilinj\u00E4rvi\r\n751,Simo\r\n753,Sipoo\r\n755,Siuntio\r\n758,Sodankyl\u00E4\r\n759,Soini\r\n761,Somero\r\n762,Sonkaj\u00E4rvi\r\n765,Sotkamo\r\n766,Sottunga\r\n768,Sulkava\r\n771,Sund\r\n777,Suomussalmi\r\n778,Suonenjoki\r\n781,Sysm\u00E4\r\n783,S\u00E4kyl\u00E4\r\n785,Vaala\r\n790,Sastamala\r\n791,Siikalatva\r\n831,Taipalsaari\r\n832,Taivalkoski\r\n833,Taivassalo\r\n834,Tammela\r\n837,Tampere\r\n844,Tervo\r\n845,Tervola\r\n846,Teuva\r\n848,Tohmaj\u00E4rvi\r\n849,Toholampi\r\n850,Toivakka\r\n851,Tornio\r\n853,Turku\r\n854,Pello\r\n857,Tuusniemi\r\n858,Tuusula\r\n859,Tyrn\u00E4v\u00E4\r\n886,Ulvila\r\n887,Urjala\r\n889,Utaj\u00E4rvi\r\n890,Utsjoki\r\n892,Uurainen\r\n893,Uusikaarlepyy\r\n895,Uusikaupunki\r\n905,Vaasa\r\n908,Valkeakoski\r\n911,Valtimo\r\n915,Varkaus\r\n918,Vehmaa\r\n921,Vesanto\r\n922,Vesilahti\r\n924,Veteli\r\n925,Vierem\u00E4\r\n927,Vihti\r\n931,Viitasaari\r\n934,Vimpeli\r\n935,Virolahti\r\n936,Virrat\r\n941,V\u00E5rd\u00F6\r\n946,V\u00F6yri\r\n976,Ylitornio\r\n977,Ylivieska\r\n980,Yl\u00F6j\u00E4rvi\r\n981,Yp\u00E4j\u00E4\r\n989,\u00C4ht\u00E4ri\r\n992,\u00C4\u00E4nekoski").Split("\r\n", StringSplitOptions.None);


            foreach (string line in data)
            {
                string[] linesplit = line.Split(",", StringSplitOptions.None);
                if (linesplit[1].ToLower().Equals(MunicipalityName.ToLower()))
                {
                    return int.Parse(linesplit[0]);
                }
            }
            return -1;
        }

        /// <summary>
        /// Converts a ClassifiedTypes value to a string.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string ClassifiedTypesToString(ClassifiedTypes type)
        {
            switch (type)
            {
                case ClassifiedTypes.All:
                    return "";
                case ClassifiedTypes.Myydaan:
                    return "sell";
                case ClassifiedTypes.Ostetaan:
                    return "buy";
                case ClassifiedTypes.Vaihdetaan:
                    return "exchange";
                case ClassifiedTypes.Vuokrataan:
                    return "for_rent";
                case ClassifiedTypes.HalutaanVuokrata:
                    return "want_rent";
                case ClassifiedTypes.Muut:
                    return "other";
                default:
                    return "";
            }
        }

        public static ClassifiedTypes StringToClassifiedType(string str, ClassifiedTypes defaultvalue = ClassifiedTypes.All)
        {
            foreach (ClassifiedTypes type in Enum.GetValues(typeof(ClassifiedTypes)))
            {
                if (str.Equals(ClassifiedTypesToString(type)))
                {
                    return type;
                }
            }
            return defaultvalue;
        }

        public static string SortingsToString(Sortings sorting)
        {
            switch (sorting)
            {
                case Sortings.BestMatching:
                    return "";
                case Sortings.Newest:
                    return "new";
                case Sortings.SmallestPrice:
                    return "pricea";
                case Sortings.LargestPrice:
                    return "priced";
                default:
                    return "";
            }
        }

        public static Sortings StringToSortings(string str, Sortings defaultvalue = Sortings.BestMatching)
        {
            foreach (Sortings sorting in Enum.GetValues(typeof(Sortings)))
            {
                if (str.Equals(SortingsToString(sorting)))
                {
                    return sorting;
                }
            }
            return defaultvalue;
        }

        public static ToriQuery ToriQueryFromToriUrl(string url, string hostname = "https://muusikoiden.net")
        {
            ToriQuery result = null;

            string[] urlsplit = HttpUtility.UrlDecode(url).Split("?", StringSplitOptions.None);
            if (urlsplit.Length == 2)
            {
                result = new ToriQuery();
                if (!urlsplit[0].StartsWith(hostname))
                {
                    result.BaseUrl = hostname + urlsplit[0];
                }

                NameValueCollection strparams = HttpUtility.ParseQueryString(HttpUtility.HtmlDecode(urlsplit[1]));
                foreach (string key in strparams.Keys)
                {
                    switch (key)
                    {
                        case "keyword":
                            result.Parameters.Add(new KeywordParameter(strparams[key]));
                            break;
                        case "province":
                            result.Parameters.Add(new ProvinceParameter(Operations.StringToProvince(strparams[key])));
                            break;
                        case "city":
                            result.Parameters.Add(new MunicipalityParameter(strparams[key]));
                            break;
                        case "location":
                            result.Parameters.Add(new RegionParameter(Operations.StringToRegion(strparams[key])));
                            break;
                        case "type":
                            result.Parameters.Add(new ClassifiedTypeParameter(Operations.StringToClassifiedType(strparams[key])));
                            break;
                        case "price_min":
                            result.Parameters.Add(new MinimumPriceParameter(int.Parse(strparams[key])));
                            break;
                        case "price_max":
                            result.Parameters.Add(new MaximumPriceParameter(int.Parse(strparams[key])));
                            break;
                        case "category":
                            result.Parameters.Add(new CategoryParameter(int.Parse(strparams[key])));
                            break;
                        case "with_image":
                            result.Parameters.Add(new OnlyWithImageParameter());
                            break;
                        case "sort":
                            result.Parameters.Add(new SortingParameter(Operations.StringToSortings(strparams[key])));
                            break;
                        case "offset":
                            result.Parameters.Add(new OffsetParameter(int.Parse(strparams[key])));
                            break;
                    }
                }

            }

            return result;
        }
    }

    /// <summary>
    /// Enumeration representing possible values for "Ilmoitustyypit"-parameter (="Classified types")
    /// </summary>
    enum ClassifiedTypes
    {
        /// <summary>
        /// "Kaikki"
        /// </summary>
        All,

        /// <summary>
        /// "Selling"
        /// </summary>
        Myydaan,

        /// <summary>
        /// "Buying"
        /// </summary>
        Ostetaan,

        /// <summary>
        /// "Willing to swap"
        /// </summary>
        Vaihdetaan,

        /// <summary>
        /// "Renting"
        /// </summary>
        Vuokrataan,

        /// <summary>
        /// "Want to rent"
        /// </summary>
        HalutaanVuokrata,

        /// <summary>
        /// "Others"
        /// </summary>
        Muut
    }

    /// <summary>
    /// Enumeration representing possible values for "Tulokset"-parameter (="Matches", or "How to sort the matches")
    /// </summary>
    enum Sortings
    {
        BestMatching,
        Newest,
        SmallestPrice,
        LargestPrice,
    }

    #endregion

    #region ResponseStuff

    class ToriResponse
    {
        private int _classifiedsQueried;
        private int _resultCount;
        private ToriClassified[] _classifieds;

        private string _nextPageUrl;

        internal ToriResponse(int queried, int resultcount)
        {
            _classifiedsQueried = queried;
            _resultCount = resultcount;
        }

        public int ClassifiedsQueried { get => _classifiedsQueried; }
        public int ResultCount { get => _resultCount; }
        public string NextPageUrl { get => _nextPageUrl; set => _nextPageUrl = value; }
        public ToriClassified[] Classifieds { get => _classifieds; set => _classifieds = value; }
    }

    #endregion
}
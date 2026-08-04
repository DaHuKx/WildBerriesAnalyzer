namespace WildBerriesAnalyzer.Domain.Models
{
    public class WildBerriesProductsResponse
    {
        public Metadata metadata { get; set; }
        public Product[] products { get; set; }
        public int total { get; set; }
    }

    public class Metadata
    {
        public string catalog_type { get; set; }
        public string catalog_value { get; set; }
        public string normquery { get; set; }
        public Search_Result search_result { get; set; }
        public string name { get; set; }
        public string rmi { get; set; }
        public string title { get; set; }
        public int rs { get; set; }
        public string[] context { get; set; }
        public string qv { get; set; }
        public string snippet { get; set; }
        public Feedbacks feedbacks { get; set; }
        public string kcl { get; set; }
        public Preset_Normquery_Map preset_normquery_map { get; set; }
    }

    public class Search_Result
    {
    }

    public class Feedbacks
    {
        public Block[] blocks { get; set; }
    }

    public class Block
    {
        public string id { get; set; }
        public string title { get; set; }
        public int line { get; set; }
        public Answer[] answers { get; set; }
    }

    public class Answer
    {
        public string id { get; set; }
        public string title { get; set; }
        public string placeholder { get; set; }
    }

    public class Preset_Normquery_Map
    {
        public string _200793656 { get; set; }
    }

    public class Product
    {
        public int id { get; set; }
        public int __sort { get; set; }
        public int ksort { get; set; }
        public long root { get; set; }
        public int kindId { get; set; }
        public string brand { get; set; }
        public int brandId { get; set; }
        public int siteBrandId { get; set; }
        public Color[] colors { get; set; }
        public int subjectId { get; set; }
        public int subjectParentId { get; set; }
        public int[] semanticId { get; set; }
        public string name { get; set; }
        public string entity { get; set; }
        public int matchId { get; set; }
        public string supplier { get; set; }
        public int supplierId { get; set; }
        public float supplierRating { get; set; }
        public int supplierFlags { get; set; }
        public int pics { get; set; }
        public int rating { get; set; }
        public float reviewRating { get; set; }
        public float nmReviewRating { get; set; }
        public int feedbacks { get; set; }
        public int nmFeedbacks { get; set; }
        public int volume { get; set; }
        public float weight { get; set; }
        public long viewFlags { get; set; }
        public int mtype { get; set; }
        public Size[] sizes { get; set; }
        public int totalQuantity { get; set; }
        public int time1 { get; set; }
        public int time2 { get; set; }
        public int wh { get; set; }
        public long dtype { get; set; }
        public int dist { get; set; }
        public string logs { get; set; }
        public Meta meta { get; set; }
        public bool isNew { get; set; }
        public int feedbackPoints { get; set; }
        public int panelPromoId { get; set; }
    }

    public class Meta
    {
        public object[] tokens { get; set; }
        public Characteristic[] characteristics { get; set; }
        public int presetId { get; set; }
    }

    public class Characteristic
    {
        public string name { get; set; }
        public string[] values { get; set; }
    }

    public class Color
    {
        public string name { get; set; }
        public int id { get; set; }
    }

    public class Size
    {
        public string name { get; set; }
        public string origName { get; set; }
        public int rank { get; set; }
        public int optionId { get; set; }
        public int wh { get; set; }
        public int time1 { get; set; }
        public int time2 { get; set; }
        public long dtype { get; set; }
        public Price price { get; set; }
        public long saleConditions { get; set; }
        public string payload { get; set; }
    }

    public class Price
    {
        public decimal basic { get; set; }
        public decimal product { get; set; }
        public decimal logistics { get; set; }
        public decimal _return { get; set; }
        public decimal cashback { get; set; }
    }



}

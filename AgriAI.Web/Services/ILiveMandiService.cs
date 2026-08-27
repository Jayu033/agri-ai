namespace AgriAI.Web.Services
{
    public class MandiRecordDto
    {
        public string Commodity { get; set; } = string.Empty;
        public string Market { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string State { get; set; } = "Gujarat";
        public string Min_Price { get; set; } = "0";
        public string Max_Price { get; set; } = "0";
        public string Modal_Price { get; set; } = "0";
        public string Arrival_Date { get; set; } = string.Empty;
        public string Variety { get; set; } = string.Empty;
        public string Grade { get; set; } = "FAQ";
    }

    public class GovApiResponseDto
    {
        public string Status { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Count { get; set; }
        public List<MandiRecordDto> Records { get; set; } = new();
    }

    public interface ILiveMandiService
    {
        Task<List<MandiRecordDto>> GetLiveGujaratMandiRatesAsync(int limit = 60);
        Task<List<MandiRecordDto>> GetRatesByCommodityAsync(string commodity);
    }
}

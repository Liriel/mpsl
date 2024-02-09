namespace mps.ViewModels
{
    public class RecommendationViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public DateTime LastCheckDate { get; set; }

        public float AvgDiff { get; set; }

        public float Weight { get; set; }

        public float Rank { get; set; }
    }
}
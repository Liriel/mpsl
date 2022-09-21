namespace mps.ViewModels
{
    public class ShoppingListItemViewModel
    {
        public int Id { get; set; }

        public int shoppingListId { get; set; }

        public string Name { get; set; }

        public string Hint { get; set; }

        public string ShortName { get; set; }

        public int? Amount { get; set; }

        public string UnitShortName { get; set; }

        public DateTime? CheckDate { get; set; }

        public string Status { get; set; }

        public bool Done { get; set; }
    }
}
namespace mps.ViewModels
{
    public class ShoppingListItemViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Hint { get; set; }

        public string ShortName { get; set; }

        public int? Amount { get; set; }

        public string UnitName { get; set; }
    }
}
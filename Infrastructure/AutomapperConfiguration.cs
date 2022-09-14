using System.Linq;
using AutoMapper;
using mps.Model;
using mps.ViewModels;

namespace mps.Infrastructure
{
    public class MyProfile : Profile
    {
        public MyProfile()
        {
            CreateMap<ShoppingList, ShoppingListViewModel>();
            CreateMap<ShoppingList, ShoppingListEditViewModel>();
            CreateMap<ShoppingListEditViewModel, ShoppingList>()
                .ForMember(dst => dst.Id, opt => opt.Ignore());

            CreateMap<ShoppingListItem, ShoppingListItemViewModel>();
            CreateMap<ShoppingListItem, ShoppingListItemEditViewModel>();
            CreateMap<ShoppingListItemEditViewModel, ShoppingListItem>()
                .ForMember(dst => dst.ShoppingListId, opt => opt.Ignore())
                .ForMember(dst => dst.Id, opt => opt.Ignore());

            CreateMap<Unit, UnitViewModel>();
            CreateMap<Unit, UnitEditViewModel>();
            CreateMap<UnitEditViewModel, Unit>()
                .ForMember(dst => dst.Id, opt => opt.Ignore());
        }

    }
}
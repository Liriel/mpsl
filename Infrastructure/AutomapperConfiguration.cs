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

            CreateMap<ShoppingListItem, ShoppingListItemViewModel>()
                .ForMember(dst => dst.Done, opt => opt.MapFrom(src => src.Status != ItemState.Open));
            CreateMap<ShoppingListItem, ShoppingListItemEditViewModel>();
            CreateMap<ShoppingListItem, ShoppingListAddViewModel>();
            CreateMap<ShoppingListItemEditViewModel, ShoppingListItem>()
                .ForMember(dst => dst.NormalizedName, opt => opt.MapFrom(src => NameHelper.GetNormalizedName(src.Name)))
                .ForMember(dst => dst.ShoppingListId, opt => opt.Ignore())
                .ForMember(dst => dst.Id, opt => opt.Ignore());
            CreateMap<ShoppingListAddViewModel, ShoppingListItem>()
                .ForMember(dst => dst.NormalizedName, opt => opt.MapFrom(src => NameHelper.GetNormalizedName(src.Name)))
                .ForMember(dst => dst.Id, opt => opt.Ignore());

            CreateMap<Unit, UnitViewModel>();
            CreateMap<Unit, UnitEditViewModel>();
            CreateMap<UnitEditViewModel, Unit>()
                .ForMember(dst => dst.Id, opt => opt.Ignore());
        }

    }
}
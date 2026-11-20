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

            CreateMap<Recommendation, RecommendationViewModel>()
                .ForMember(dst => dst.Name, opt => opt.MapFrom(src => src.ShoppingListItem.Name))
                .ForMember(dst => dst.Id, opt => opt.MapFrom(src => src.ShoppingListItemId));

            CreateMap<ShoppingListItem, ShoppingListItemViewModel>()
                .ForMember(dst => dst.Done, opt => opt.MapFrom(src => src.Status != ItemState.Open));
            CreateMap<ShoppingListItem, ShoppingListItemEditViewModel>();
            CreateMap<ShoppingListItem, ShoppingListAddViewModel>();
            CreateMap<ShoppingListItemEditViewModel, ShoppingListItem>()
                .ForMember(dst => dst.Name, opt => opt.MapFrom(src => src.Name.Trim()))
                .ForMember(dst => dst.NormalizedName, opt => opt.MapFrom(src => NameHelper.GetNormalizedName(src.Name)))
                // ShortName is [Required] on the entity but optional on the view model, and it is
                // generated on create when left blank. An update that omits it must therefore keep
                // the stored one rather than null it out and fail at the database.
                .ForMember(dst => dst.ShortName, opt => opt.Condition((src, dst, srcMember) => srcMember != null))
                // null Kind = not supplied, keep the stored one
                .ForMember(dst => dst.Kind, opt => opt.Condition(src => src.Kind != null))
                .ForMember(dst => dst.ShoppingListId, opt => opt.Ignore())
                .ForMember(dst => dst.Id, opt => opt.Ignore());
            CreateMap<ShoppingListAddViewModel, ShoppingListItem>()
                // AddItem decides the kind (explicit, "!" prefix or classifier)
                .ForMember(dst => dst.Kind, opt => opt.Ignore())
                .ForMember(dst => dst.Name, opt => opt.MapFrom(src => src.Name.Trim()))
                .ForMember(dst => dst.NormalizedName, opt => opt.MapFrom(src => NameHelper.GetNormalizedName(src.Name)))
                .ForMember(dst => dst.Id, opt => opt.Ignore());

            CreateMap<Unit, UnitViewModel>();
            CreateMap<Unit, UnitEditViewModel>();
            CreateMap<UnitEditViewModel, Unit>()
                .ForMember(dst => dst.Id, opt => opt.Ignore());

            // Declutter mappings
            CreateMap<Services.DeclutterCandidate, DeclutterCandidateViewModel>()
                .ForMember(dst => dst.Id, opt => opt.MapFrom(src => src.Item.Id))
                .ForMember(dst => dst.ShoppingListId, opt => opt.MapFrom(src => src.Item.ShoppingListId))
                .ForMember(dst => dst.Name, opt => opt.MapFrom(src => src.Item.Name))
                .ForMember(dst => dst.ShortName, opt => opt.MapFrom(src => src.Item.ShortName))
                .ForMember(dst => dst.Kind, opt => opt.MapFrom(src => src.Item.Kind.ToString()));
        }

    }
}
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
            // CreateMap<Customer, CustomerListViewModel>();
            // CreateMap<Customer, CustomerEditViewModel>();
            // CreateMap<CustomerEditViewModel, Customer>()
            //     .ForMember(dst => dst.Id, opt => opt.Ignore());
        }

    }
}
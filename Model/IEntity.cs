namespace mps.Model{
    // can't use a BaseEntity like a normal person because sqlite ef migration
    // does not look for DataAnnotations on base classes
    // TODO: change to EntityBase class for mssql
    public interface IEntity{
        int Id {get;set;}
    }
}
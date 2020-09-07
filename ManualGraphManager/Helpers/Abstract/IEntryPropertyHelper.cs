namespace EntityFrameworkCore.GraphManager.ManualGraphManager.Helpers.Abstract
{
    public interface IEntryPropertyHelper<TProperty>
    {
        bool IsModified { get; set; }
        TProperty Value { get; }
    }
}

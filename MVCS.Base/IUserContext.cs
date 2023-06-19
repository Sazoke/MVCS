namespace MVCS.Base;

public interface IUserContext<out TKey>
{
    public TKey? CurrentUserId { get; }
}
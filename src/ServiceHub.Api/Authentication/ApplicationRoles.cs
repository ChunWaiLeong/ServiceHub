namespace ServiceHub.Api.Authentication;

public static class ApplicationRoles
{
    public const string Customer = "Customer";
    public const string BusinessOwner = "BusinessOwner";
    public const string Admin = "Admin";
    public static readonly string[] All = [Customer, BusinessOwner, Admin];
}

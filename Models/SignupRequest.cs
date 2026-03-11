namespace Label_CRM_demo.Models;

public sealed record SignupRequest(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Email,
    string Password);

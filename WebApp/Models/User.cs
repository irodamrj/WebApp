using System;
using System.Collections.Generic;

namespace WebApp.Models;

public partial class User
{
    public string Name { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public long Phone { get; set; }

    public string IsVerified { get; set; } = null!;

    public string VerificationToken { get; set; } = null!;

    public long VerificationCode { get; set; }
}

# User Manager App with ASP.NET Core MVC Identity – Step‑by‑Step Tutorial

Build a production‑ready **User Manager App** in ASP.NET Core MVC using **ASP.NET Identity** with:

- Roles: **Admin, Manager, User**
- **Email confirmation** on registration
- **Admin/Manager approval** required before login (`IsApproved`)
- **Role management** (assign/change roles)
- **Password reset via email**
- **Gmail SMTP** for email service
- Extended `IdentityUser` → `ApplicationUser`
- Tooling: **Visual Studio**, **SQL Server**, **.NET 8** (works with .NET 7 as well)

---

## 0) Prerequisites

- Visual Studio 2022 (latest) with ASP.NET and web workload
- .NET SDK 8 (or 7)
- SQL Server (LocalDB is fine)
- A Gmail account with **App Password** (2‑step verification must be on)
- Basic familiarity with MVC and EF Core migrations

> **Why an App Password?** Google blocks basic auth with real passwords. Create one at `Google Account → Security → App passwords` and use that 16‑digit code as your SMTP password.

---

## 1) Create the MVC Project

1. **File → New → Project →** *ASP.NET Core Web App (Model-View-Controller)*
2. Name: `UserManagerApp`
3. Framework: **.NET 8**
4. Authentication: **Individual Accounts** (preferred). If you pick **None**, you will scaffold Identity in step 9.

This template includes Identity, Razor Class Library UI, EF Core, and cookie authentication.

---

## 2) Connection String

Edit `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=UserManagerAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;"
}
```

> Adjust for your SQL Server instance if needed.

---

## 3) Create the Extended User: `ApplicationUser`

**Models/ApplicationUser.cs**

```csharp
using Microsoft.AspNetCore.Identity;

namespace UserManagerApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public bool IsApproved { get; set; } = false; // Admin/Manager approval gate
    }
}
```

---

## 4) DbContext using the Custom User

**Models/ApplicationDbContext.cs** (or wherever your context is; by default it’s created under *Data* or *Models*):

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace UserManagerApp.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }
    }
}
```

> Ensure your project uses **`IdentityDbContext<ApplicationUser>`**, not `IdentityDbContext<IdentityUser>`.

---

## 5) Configure Identity in `Program.cs`

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UserManagerApp.Models;

var builder = WebApplication.CreateBuilder(args);

// 1) EF Core + SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2) Identity with custom user + roles
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true; // Email verification required
    // Optional password policy examples:
    // options.Password.RequiredLength = 6;
    // options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages(); // needed for Identity UI

app.Run();
```

---

## 6) Configure Email (Gmail SMTP)

**appsettings.json**

```json
"EmailSettings": {
  "SmtpServer": "smtp.gmail.com",
  "Port": 587,
  "SenderName": "User Manager App",
  "SenderEmail": "your-email@gmail.com",
  "Username": "your-email@gmail.com",
  "Password": "<your-16-digit-app-password>"
}
```

**Services/EmailSender.cs**

```csharp
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net;
using System.Net.Mail;

namespace UserManagerApp.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        public EmailSender(IConfiguration config) => _config = config;

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var smtp = _config["EmailSettings:SmtpServer"];
            var port = int.Parse(_config["EmailSettings:Port"]!);
            var from = _config["EmailSettings:SenderEmail"];
            var user = _config["EmailSettings:Username"];
            var pass = _config["EmailSettings:Password"];

            using var client = new SmtpClient(smtp, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(user, pass)
            };

            var mail = new MailMessage(from!, email, subject, htmlMessage) { IsBodyHtml = true };
            return client.SendMailAsync(mail);
        }
    }
}
```

Register the service in **Program.cs**:

```csharp
using Microsoft.AspNetCore.Identity.UI.Services;
using UserManagerApp.Services;

builder.Services.AddTransient<IEmailSender, EmailSender>();
```

> **Tip (don’t commit secrets):**
> Use **User Secrets** in development:
> ```bash
> dotnet user-secrets init
> dotnet user-secrets set "EmailSettings:Password" "<app-password>"
> ```

---

## 7) Create the Database

Open **Package Manager Console**:

```powershell
Add-Migration InitIdentity
Update-Database
```

This creates all Identity tables using your `ApplicationUser`.

---

## 8) Seed Roles (Admin, Manager, User)

**Data/DbInitializer.cs**

```csharp
using Microsoft.AspNetCore.Identity;

namespace UserManagerApp.Data
{
    public static class DbInitializer
    {
        public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            var roles = new[] { "Admin", "Manager", "User" };
            foreach (var role in roles)
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}
```

Run the seeding at startup (**Program.cs**, after `builder.Build()`):

```csharp
using UserManagerApp.Data;

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await DbInitializer.SeedRolesAsync(roleManager);
}
```

---

## 9) Scaffold Identity UI (if needed) & Pages We’ll Edit

If your template didn’t include UI, scaffold it:

- **Add → New Scaffolded Item → Identity**
- Select: **Register, Login, Account.Manage** (and optionally Forgot/Reset Password)
- Place under: `Areas/Identity/Pages/Account/`

We will edit:
- `Register.cshtml` & `Register.cshtml.cs`
- `Login.cshtml.cs`

---

## 10) Add `FullName` to Registration UI

**Areas/Identity/Pages/Account/Register.cshtml** – add above Email field:

```html
<div class="form-group">
    <label asp-for="Input.FullName"></label>
    <input asp-for="Input.FullName" class="form-control" />
    <span asp-validation-for="Input.FullName" class="text-danger"></span>
</div>
```

Ensure the **InputModel** includes `FullName`:

**Areas/Identity/Pages/Account/Register.cshtml.cs** (inside `RegisterModel.InputModel`):

```csharp
[Required]
[Display(Name = "Full Name")]
public string FullName { get; set; }
```

---

## 11) Persist `FullName` and Default `IsApproved = false`

In **`Register.cshtml.cs`**, inside `OnPostAsync` before `CreateAsync`:

```csharp
if (ModelState.IsValid)
{
    var user = CreateUser();

    await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
    await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

    // Custom fields
    user.FullName = Input.FullName;
    user.IsApproved = false; // must be approved manually

    var result = await _userManager.CreateAsync(user, Input.Password);
    // ... remainder unchanged
}
```

Run migrations if you added properties later:

```powershell
Add-Migration AddFullNameAndIsApproved
Update-Database
```

---

## 12) Enforce Email Confirmation & Admin Approval on Login

Open **Areas/Identity/Pages/Account/Login.cshtml.cs** and ensure the PageModel uses `ApplicationUser`:

```csharp
private readonly SignInManager<ApplicationUser> _signInManager;
private readonly UserManager<ApplicationUser> _userManager;

public LoginModel(SignInManager<ApplicationUser> signInManager,
                  UserManager<ApplicationUser> userManager)
{
    _signInManager = signInManager;
    _userManager = userManager;
}
```

Inside `OnPostAsync` **before** calling `PasswordSignInAsync`:

```csharp
var user = await _userManager.FindByEmailAsync(Input.Email);
if (user != null)
{
    if (!await _userManager.IsEmailConfirmedAsync(user))
    {
        ModelState.AddModelError(string.Empty, "You must confirm your email before logging in.");
        return Page();
    }

    if (!user.IsApproved)
    {
        ModelState.AddModelError(string.Empty, "Your account is pending approval by Admin/Manager.");
        return Page();
    }
}
```

Then proceed with:

```csharp
var result = await _signInManager.PasswordSignInAsync(
    Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
```

> If you see `No service for type 'UserManager<IdentityUser>'`, replace all `IdentityUser` generics with **`ApplicationUser`** in the page.

---

## 13) Admin/Manager – Approve Users & Manage Roles

**Controllers/AdminController.cs**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UserManagerApp.Models;

namespace UserManagerApp.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(UserManager<ApplicationUser> userManager,
                               RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public IActionResult PendingUsers()
        {
            var users = _userManager.Users.Where(u => !u.IsApproved).ToList();
            return View(users);
        }

        public async Task<IActionResult> ApproveUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsApproved = true;
                await _userManager.UpdateAsync(user);
            }
            return RedirectToAction(nameof(PendingUsers));
        }

        public IActionResult AllUsers()
        {
            var users = _userManager.Users.ToList();
            return View(users);
        }

        public async Task<IActionResult> AssignRole(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && await _roleManager.RoleExistsAsync(role))
            {
                // Remove from other app roles if you want one-at-a-time role
                // var currentRoles = await _userManager.GetRolesAsync(user);
                // await _userManager.RemoveFromRolesAsync(user, currentRoles);

                await _userManager.AddToRoleAsync(user, role);
            }
            return RedirectToAction(nameof(AllUsers));
        }
    }
}
```

**Views/Admin/PendingUsers.cshtml**

```cshtml
@model IEnumerable<UserManagerApp.Models.ApplicationUser>
<h2>Pending Users</h2>
<table class="table">
    <thead>
        <tr>
            <th>Email</th>
            <th>Full Name</th>
            <th>Action</th>
        </tr>
    </thead>
    <tbody>
    @foreach (var user in Model)
    {
        <tr>
            <td>@user.Email</td>
            <td>@user.FullName</td>
            <td>
                <a asp-action="ApproveUser" asp-route-id="@user.Id" class="btn btn-success">Approve</a>
            </td>
        </tr>
    }
    </tbody>
</table>
```

**Views/Admin/AllUsers.cshtml**

```cshtml
@model IEnumerable<UserManagerApp.Models.ApplicationUser>
<h2>All Users</h2>
<table class="table">
    <thead>
        <tr>
            <th>Email</th>
            <th>Full Name</th>
            <th>Approved</th>
            <th>Assign Role</th>
        </tr>
    </thead>
    <tbody>
    @foreach (var user in Model)
    {
        <tr>
            <td>@user.Email</td>
            <td>@user.FullName</td>
            <td>@user.IsApproved</td>
            <td>
                <a asp-action="AssignRole" asp-route-userId="@user.Id" asp-route-role="Admin" class="btn btn-danger">Admin</a>
                <a asp-action="AssignRole" asp-route-userId="@user.Id" asp-route-role="Manager" class="btn btn-primary">Manager</a>
                <a asp-action="AssignRole" asp-route-userId="@user.Id" asp-route-role="User" class="btn btn-secondary">User</a>
            </td>
        </tr>
    }
    </tbody>
</table>
```

---

## 14) Seed the First Admin User

Add this to the startup seeding block (after role seeding):

```csharp
using Microsoft.AspNetCore.Identity;
using UserManagerApp.Models;

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await DbInitializer.SeedRolesAsync(roleManager);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    string adminEmail = "admin@usermanager.com";
    string adminPassword = "Admin@123"; // change in production

    var admin = await userManager.FindByEmailAsync(adminEmail);
    if (admin == null)
    {
        var adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "Super Admin",
            EmailConfirmed = true,
            IsApproved = true
        };

        var create = await userManager.CreateAsync(adminUser, adminPassword);
        if (create.Succeeded)
            await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}
```

---

## 15) Add Admin Links to the Navbar (optional)

**Views/Shared/_Layout.cshtml** (inside the logged‑in block):

```cshtml
@using Microsoft.AspNetCore.Identity
@inject SignInManager<UserManagerApp.Models.ApplicationUser> SignInManager
@inject UserManager<UserManagerApp.Models.ApplicationUser> UserManager

@if (SignInManager.IsSignedIn(User) && (User.IsInRole("Admin") || User.IsInRole("Manager")))
{
    <li class="nav-item">
        <a class="nav-link" asp-controller="Admin" asp-action="PendingUsers">Pending Users</a>
    </li>
    <li class="nav-item">
        <a class="nav-link" asp-controller="Admin" asp-action="AllUsers">All Users</a>
    </li>
}
```

---

## 16) Forgot/Reset Password Flow

If you scaffolded **ForgotPassword** and **ResetPassword** pages, Identity will automatically:

- Generate a reset token
- Email the user a link (via our `IEmailSender`)
- Allow a new password to be set

No extra code needed beyond working email configuration.

---

## 17) Test the Full Flow (Checklist)

1. Run the app → it seeds roles and first Admin.
2. Log in as `admin@usermanager.com` / `Admin@123`.
3. Go to **Pending Users** (should be empty initially).
4. Create a new user via **Register**:
   - Enter **Full Name**, Email, Password.
   - Check email and click **Confirm Email**.
   - Try logging in → should be blocked with *pending approval* message.
5. Log back in as Admin → **Pending Users** → **Approve** the user.
6. User can now log in.
7. In **All Users**, try assigning roles.
8. Try **Forgot Password** to confirm emails send and reset works.

---

## 18) Troubleshooting

**❌ `InvalidOperationException: No service for type 'UserManager<IdentityUser>'`**
- Some file still injects `UserManager<IdentityUser>` instead of `UserManager<ApplicationUser>`.
- Replace all `IdentityUser` generics with **`ApplicationUser`** and rebuild.

**❌ Emails not sending**
- Ensure App Password is correct.
- SMTP: `smtp.gmail.com`, Port `587`, SSL enabled.
- Some corporate networks block SMTP; test from another network.

**❌ Login still blocked after email confirmation**
- Remember you also enforce `IsApproved` – approve the user in Admin.

**❌ Database doesn’t have new columns**
- Run migrations after adding properties to `ApplicationUser`.

**❌ 404 for Admin pages**
- Check controller/action names and add links in navbar.
- Ensure you are logged in as Admin/Manager (authorization attribute).

---

## 19) Production Tips & Enhancements

- Use **SendGrid** or another provider in production.
- Add **2FA** (`options.SignIn.RequireConfirmedEmail = true` + 2FA UI pages).
- Add **audit logging** for approvals/role changes.
- Create an **Admin Dashboard** page that aggregates Pending/All Users.
- Move secrets to environment variables or a secure vault.
- Harden password policy and configure lockout options.

---

## 20) Reference – Minimal File Index

- `Models/ApplicationUser.cs`
- `Models/ApplicationDbContext.cs`
- `Services/EmailSender.cs`
- `Data/DbInitializer.cs`
- `Controllers/AdminController.cs`
- `Views/Admin/PendingUsers.cshtml`
- `Views/Admin/AllUsers.cshtml`
- `Areas/Identity/Pages/Account/Register.cshtml`
- `Areas/Identity/Pages/Account/Register.cshtml.cs`
- `Areas/Identity/Pages/Account/Login.cshtml.cs`
- `Views/Shared/_Layout.cshtml` (optional links)
- `Program.cs`
- `appsettings.json`

---

### You’re Done!
You now have a robust Identity setup with **email verification**, **admin approval**, and **role management**. This is the foundation most real-world ASP.NET Core apps build on—great work!

---

# Dabananda Mitra
Email: dabananda.dev@gmail.com <br>
Portfolio: https://www.dmitra.me <br>
WhatsApp: +8801304080014 <br>
LinkedIn: https://www.linkedin.com/in/dabananda/ <br>
Facebook: https://fb.com/imdmitra
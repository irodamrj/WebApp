using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit.Text;
using MimeKit;
using WebApp.Models;
using WebApp.Models.DTO;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Net.Http.Headers;

namespace WebApp.Controllers
{
    public class AuthController : Controller
    {
        private readonly UserContext _context;
        private readonly IConfiguration _configuration;
        public AuthController(UserContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Home()
        {
            return View("Home");
        }

        [HttpPost]
        public async Task<ActionResult<UserDTO>> LoginPost(UserDTO userDTO)
        {
            if (ModelState.IsValid)
            {
                User? user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == userDTO.Email);
                if (user == null)
                {
                    ModelState.AddModelError("", "No user with given email");
                }
                else if (user.IsVerified == "False" || user.IsVerified == "True")
                {
                    ModelState.AddModelError("", "Please verify your acount before log in");
                }
                else
                {
                    bool isvalid = BCrypt.Net.BCrypt.Verify(userDTO.Password, user.Password);
                    if (isvalid)
                    {
                        ViewBag.msg = "Logged in successfully";
                        return RedirectToAction("Home");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Invalid credentials");
                    }
                }
            }
            return View("Login");
        }


        [HttpGet]
        public IActionResult Login()
        {
            return View("Login");
        }

        [HttpGet]
        public IActionResult Signup()
        {
            return View("Signup");
        }

        [HttpPost]
        public async Task<IActionResult> SignupPost(SignupDTO user)
        {
            if (ModelState.IsValid)
            {
                bool isExist = await _context.Users.AsNoTracking().AnyAsync(u => u.Email == user.Email);
                if (isExist)
                    ModelState.AddModelError("duplicate", "Email is already exist");
                else
                {
                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(user.Password, 10);
                    string token = GenerateToken();

                    int randomSixDigitNumber = GenerateCode();

                    User newUser = new User()
                    {
                        Email = user.Email,
                        Password = hashedPassword,
                        Name = user.Name,
                        LastName = user.LastName,
                        Phone = user.Phone,
                        IsVerified = "False",
                        VerificationToken = token,
                        VerificationCode = (long)randomSixDigitNumber
                    };


                    try
                    {
                        //send mail
                        var email = new MimeMessage();
                        email.From.Add(MailboxAddress.Parse(_configuration.GetSection("Yandex").GetSection("Username").Value));
                        email.To.Add(MailboxAddress.Parse(user.Email));
                        email.Subject = "Verification Email";
                        email.Body = new TextPart(TextFormat.Html) { Text = $"http://localhost:5048/Auth/VerifyEmail?token={token}" };
                        using var smtp = new MailKit.Net.Smtp.SmtpClient();
                        smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;
                        smtp.AuthenticationMechanisms.Remove("XOAUTH2");
                        await smtp.ConnectAsync("smtp.yandex.com", 465, useSsl: true);
                        await smtp.AuthenticateAsync(_configuration.GetSection("Yandex").GetSection("Username").Value, _configuration.GetSection("Yandex").GetSection("Password").Value);
                        await smtp.SendAsync(email);
                        await smtp.DisconnectAsync(true);

                        //send verification code with sms
                        //string message = $"Your verification number is {randomSixDigitNumber}";
                        //await _netGsmService.SendSmsAsync(user.Phone, message);

                        await _context.Users.AddAsync(newUser);
                        await _context.SaveChangesAsync();
                        ViewBag.msg = "Signed up successfully. Please confirm your email and phone number before loggin in.";
                        return RedirectToAction("Home");
                    }
                    catch (Exception e)
                    {
                        ModelState.AddModelError("SMS", $"While trying to send mail {e.Message} occured.");
                    }

                }
            }
            return View("signup");
        }

        [HttpGet]
        public IActionResult VerifyPhone()
        {
            return View("Verify");
        }


        [HttpPost]
        public async Task<IActionResult> VerifyPhonePost(PhoneVerificationDTO dto)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Phone == dto.Phone && u.VerificationCode == dto.VerificationCode);
                if (user == null)
                    ModelState.AddModelError("", "Wrong verification code or phone number");
                else
                {
                    if (user.IsVerified == "False")
                        user.IsVerified = "True";
                    else if (user.IsVerified == "True")
                        user.IsVerified = "True-True";
                    await _context.SaveChangesAsync();
                    ViewBag.msg = "Phone verified successfully";
                }

            }
            return View("Verify");
        }


        [HttpGet]
        public async Task<IActionResult> VerifyEmail([FromQuery(Name = "token")] string token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.VerificationToken == token);
            if (user == null)
                ViewBag.msg = "wrong token";
            else
            {
                if (user.IsVerified == "False")
                    user.IsVerified = "True";
                else if (user.IsVerified == "True")
                    user.IsVerified = "True-True";
                await _context.SaveChangesAsync();
                ViewBag.msg = "Email verified successfully";
            }

            return View("VerifyEmail");

        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            return View("ResetPassword");
        }

        [HttpPost]
        public async Task<IActionResult> ResetPasswordPost(ResetPasswordDTO resetDTO)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == resetDTO.Email);
                if (user == null)
                    ModelState.AddModelError("", "No user with given email");

                else
                {
                    bool isValid = BCrypt.Net.BCrypt.Verify(resetDTO.OldPassword, user.Password);
                    if (!isValid)
                        ModelState.AddModelError("", "Invalid credentials");
                    else
                    {
                        user.Password = BCrypt.Net.BCrypt.HashPassword(resetDTO.Password, 10);
                        await _context.SaveChangesAsync();
                        ViewBag.msg = "password changed successfully";
                    }
                }
            }
            return View("ResetPassword");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View("ForgotPassword");
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPasswordPost(long phone)
        {

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Phone == phone);
            int randomSixDigitNumber = GenerateCode();
            if (user != null)
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(randomSixDigitNumber.ToString(), 10);
                await _context.SaveChangesAsync();

            }
            string message = $"Your one time password is {randomSixDigitNumber}";
            string username = _configuration.GetSection("NetGSM").GetSection("ApiUsername").Value!;
            string password = _configuration.GetSection("NetGSM").GetSection("ApiPassword").Value!;
            string fromNumber = "SenderNumber";
            string apiUrl = "https://api.netgsm.com.tr/sms/send/get/";
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var formData = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("usercode", username),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("gsmno", phone.ToString()),
                new KeyValuePair<string, string>("message", message),
                new KeyValuePair<string, string>("msgheader", fromNumber)
                });
                try
                {
                    HttpResponseMessage response = await client.PostAsJsonAsync(apiUrl, formData);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        ViewBag.msg = "SMS sent successfully.";
                    }
                    else
                    {
                        ViewBag.msg = "SMS sending failed. Response: " + responseBody;
                    }
                }
                catch (Exception ex)
                {
                    ViewBag.msg = "Internal error " + ex.Message;
                }
            }

            return View("ForgotPassword");

        }



        private string GenerateToken()
        {
            {
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
                Random random = new Random();

                char[] stringChars = new char[10];
                for (int i = 0; i < stringChars.Length; i++)
                {
                    stringChars[i] = chars[random.Next(chars.Length)];
                }

                return new string(stringChars);
            }
        }

        private int GenerateCode()
        {
            Random random = new Random();
            int minValue = 100000;
            int maxValue = 999999;
            int randomSixDigitNumber = random.Next(minValue, maxValue + 1);
            return randomSixDigitNumber;
        }




    }
}

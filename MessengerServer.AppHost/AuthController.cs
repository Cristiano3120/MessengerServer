using DotNetEnv;
using MessengerServer.AppHost.APIResponse;
using MessengerServer.AppHost.Logging;
using MessengerServer.AppHost.UserResources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Net;
using System.Net.Mail;

namespace MessengerServer.AppHost
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly string _connStr;
        private readonly AuthControllerFields _fields;

        public AuthController(AuthControllerFields fields, string connStr)
        {
            _fields = fields;
            _connStr = connStr;
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            APIResponse<bool> aPIResponse = new()
            {
                Data = true,
                IsSuccess = true,
            };

            return Ok(aPIResponse);
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateAcc([FromBody] User user)
        {
            byte[] emailHash = DatabaseCryptography.Hash(user.Email!);
            SnowflakeGenerator snowflakeGenerator = new(0);

            (bool emailInUse, bool usernameInUse) = await CheckUserCreationAsync(emailHash, user.Username!);

            if (emailInUse)
            {
                return Conflict(new APIResponse<object>
                {
                    Data = null,
                    IsSuccess = false,
                    APIError = new APIError
                    {
                        StatusCode = HttpStatusCode.Conflict,
                        Message = "Email already in use",
                    },
                    FieldErrors =
                    [
                        new()
                        {
                            Field = nameof(user.Email),
                            Message = "Email already in use",
                        },
                    ],
                });
            }
            else if (usernameInUse)
            {
                return Conflict(new APIResponse<object>
                {
                    Data = null,
                    IsSuccess = false,
                    APIError = new APIError
                    {
                        StatusCode = HttpStatusCode.Conflict,
                        Message = "Username already in use",
                    },
                    FieldErrors =
                    [
                        new()
                        {
                            Field = nameof(user.Username),
                            Message = "Username already in use",
                        },
                    ],
                });
            }

            _ = Task.Run(async () =>
            {
                EncryptedUser encryptedUser = default!;
                try
                {
                    byte[] passwordHash = DatabaseCryptography.Hash(user.Password!);
                    byte[] emailEncrypted = DatabaseCryptography.Encrypt(user.Email!);

                    encryptedUser = new()
                    {
                        EmailHash = emailHash,
                        PasswordHash = passwordHash,
                        Email = emailEncrypted,
                        Username = user.Username!,
                        Biography = user.Biography!,
                        ProfilPicture = user.ProfilPicture,
                        TFAEnabled = user.TFAEnabled!.Value,
                        Birthday = user.Birthday,
                        Id = snowflakeGenerator.GenerateId(),
                    };

                    DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                        .UseNpgsql(_connStr).Options;

                    await using PostgresDbContext dbContext = new(options);

                    _ = dbContext.Users.Add(encryptedUser);
                    _ = dbContext.SaveChanges();

                    await StartVerificationProcess(user.Email!, user.Username, encryptedUser.Id);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, CallerInfos.Create());
                    await TryToSaveUserAsync(encryptedUser);
                }
            });

            APIResponse<ulong> aPIResponse = new()
            {
                Data = snowflakeGenerator.GenerateId(),
                IsSuccess = true,
            };

            return Ok(aPIResponse);
        }

        private async Task<(bool emailExists, bool usernameExists)> CheckUserCreationAsync(byte[] emailHash, string username)
        {
            await using NpgsqlConnection conn = new(_connStr);
            await conn.OpenAsync();

            await using NpgsqlCommand cmd = new(@"SELECT
                EXISTS (SELECT 1 FROM ""Users"" WHERE ""EmailHash"" = @email) AS email_exists,
                EXISTS (SELECT 1 FROM ""Users"" WHERE ""Username"" = @username) AS username_exists;"
            , conn);

            _ = cmd.Parameters.AddWithValue("email", emailHash);
            _ = cmd.Parameters.AddWithValue("username", username);

            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetBoolean(reader.GetOrdinal("email_exists")),
                        reader.GetBoolean(reader.GetOrdinal("username_exists")));
            }

            return (false, false);
        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync([FromBody] LoginData loginData)
        {
            byte[] emailHash = DatabaseCryptography.Hash(loginData.Email);
            byte[] passwordHash = DatabaseCryptography.Hash(loginData.Password);

            DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql(_connStr).Options;

            await using PostgresDbContext dbContext = new(options);

            EncryptedUser? encryptedUser = await dbContext.Users
                .FirstOrDefaultAsync(x => x.EmailHash == emailHash);

            if (encryptedUser is null || encryptedUser is not null && encryptedUser.PasswordHash != passwordHash)
            {
                APIResponse<object> aPIResponse = new()
                {
                    Data = null,
                    IsSuccess = false,
                    APIError = new APIError
                    {
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "Invalid email or password",
                    },
                };

                return NotFound(aPIResponse);
            }

            bool? tFAEnabled = loginData.IsAutoLogin
                ? null
                : encryptedUser!.TFAEnabled;

            User user = new()
            {
                Id = encryptedUser!.Id,
                Username = encryptedUser.Username,
                Biography = encryptedUser.Biography,
                ProfilPicture = encryptedUser.ProfilPicture,
                TFAEnabled = tFAEnabled,
            };

            if (tFAEnabled is true)
            {
                int verificationCode = Random.Shared.Next(1_000_000, 10_000_000);
                Timer timer = new(DeleteVerificationCodeEntry, user.Id, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);

                (int, Timer) tuple = (verificationCode, timer);
                _fields.VerificationCodes.Add(encryptedUser.Id, tuple);
            }

            return Ok(user);
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyAsync([FromBody] Verify verify)
        {
            if (_fields.VerificationCodes.TryGetValue(verify.UserId, out (int code, Timer timer) value))
            {
                if (value.code != verify.VerificationCode)
                {
                    return BadRequest(new APIResponse<User>
                    {
                        Data = null,
                        IsSuccess = false,
                    });
                }

                DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                        .UseNpgsql(_connStr).Options;

                await using PostgresDbContext dbContext = new(options);

                EncryptedUser? encryptedUser = await dbContext.Users
                        .FirstOrDefaultAsync(x => x.Id == verify.UserId);

                if (encryptedUser is null)
                {
                    return NotFound(new APIResponse<User>
                    {
                        Data = null,
                        IsSuccess = false,
                        APIError = new APIError
                        {
                            StatusCode = HttpStatusCode.NotFound,
                            Message = "User not found. ID invalid",
                        },
                    });
                }

                User user = new()
                {
                    Id = encryptedUser.Id,
                    Username = encryptedUser.Username,
                    Biography = encryptedUser.Biography,
                    ProfilPicture = encryptedUser.ProfilPicture,
                    TFAEnabled = encryptedUser.TFAEnabled,
                    Birthday = encryptedUser.Birthday,
                };

                return Ok(new APIResponse<User>
                {
                    Data = user,
                    IsSuccess = true,
                });
            }

            return NotFound(new APIResponse<object>()
            {
                Data = null,
                IsSuccess = false,
                APIError = new APIError
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Message = "No verification code found for this user",
                },
            });
        }

        private async Task StartVerificationProcess(string email, string username, ulong id)
        {
            (int code, Timer timer) = (Random.Shared.Next(10_000_000, 99_999_999),
                new Timer(DeleteVerificationCodeEntry, id, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan));

            _fields.VerificationCodes.Add(id, (code, timer));

            const string emailAttribute = "EMAIL";
            const string appPasswordAttribute = "EMAIL_PASSWORD";

            MailMessage mail = new()
            {
                From = new MailAddress(Env.GetString(emailAttribute)),
                To = { email! },
                Body = $"Hello {username}! Your verification code is: {code}",
            };

            using SmtpClient smtp = new("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(Env.GetString(emailAttribute), Env.GetString(appPasswordAttribute)),
                EnableSsl = true
            };

            //await smtp.SendMailAsync(mail);

            Logger.Log($"Sent {code} to {email}");
        }

        private void DeleteVerificationCodeEntry(object? state)
        {
            ulong userId = (ulong)state!;
            _ = _fields.VerificationCodes.Remove(userId);

            DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                        .UseNpgsql(_connStr).Options;

            using PostgresDbContext dbContext = new(options);
            EncryptedUser? user = dbContext.Users.FirstOrDefault(x => x.Id == userId);

            if (user is not null)
            {
                _ = dbContext.Users.Remove(user);
            }

            Logger.Log($"Deleted code for user {userId}");
        }

        private async Task SaveFailedUsersAsync()
        {
            TimeSpan timeToWait = TimeSpan.FromSeconds(30);

            while (true)
            {
                try
                {
                    if (_fields.FailedUsers.Count == 0)
                    {
                        break;
                    }

                    DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                        .UseNpgsql(_connStr).Options;

                    await using PostgresDbContext dbContext = new(options);

                    dbContext.Users.AddRange(_fields.FailedUsers);
                    _ = dbContext.SaveChanges();
                    _fields.FailedUsers.Clear();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, CallerInfos.Create());
                }

                await Task.Delay(timeToWait);
            }
        }

        private async Task TryToSaveUserAsync(EncryptedUser user)
        {
            try
            {
                TimeSpan timeToWait = TimeSpan.FromSeconds(5);
                await Task.Delay(timeToWait);

                DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                        .UseNpgsql(_connStr).Options;

                await using PostgresDbContext dbContext = new(options);

                _ = dbContext.Users.Add(user);
                _ = dbContext.SaveChanges();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, CallerInfos.Create());
                _fields.FailedUsers.Add(user);
                _fields.SaveFailedUsersTask ??= Task.Run(SaveFailedUsersAsync);
            }
        }
    }
}

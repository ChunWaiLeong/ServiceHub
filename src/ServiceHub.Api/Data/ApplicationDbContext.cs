using Microsoft.EntityFrameworkCore;

namespace ServiceHub.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options);

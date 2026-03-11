using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;

namespace STASIS.Pages.Administration
{
    [Authorize(Roles = "Admin")]
    public class AuditModel : PageModel
    {
        private readonly StasisDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private const int PageSize = 50;

        public AuditModel(StasisDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)]
        public string? TableName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? UserId { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DateFrom { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DateTo { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;

        public List<AuditLog> AuditLogs { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public List<string> TableNames { get; set; } = new();
        public List<IdentityUser> Users { get; set; } = new();

        public async Task OnGetAsync()
        {
            TableNames = await _context.AuditLogs
                .Select(a => a.TableName)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            Users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();

            var query = _context.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(TableName))
                query = query.Where(a => a.TableName == TableName);

            if (!string.IsNullOrEmpty(UserId))
                query = query.Where(a => a.ChangedByUserId == UserId);

            if (DateFrom.HasValue)
                query = query.Where(a => a.Timestamp >= DateFrom.Value.ToUniversalTime());

            if (DateTo.HasValue)
                query = query.Where(a => a.Timestamp < DateTo.Value.ToUniversalTime().AddDays(1));

            TotalCount = await query.CountAsync();

            AuditLogs = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using EmailCollector.Models;

namespace EmailCollector.Pages
{
    public class ListModel : PageModel
    {
        private readonly EmailCollectorContext _context;

        public ListModel(EmailCollectorContext context)
        {
            _context = context;
        }

        public IList<Email> Email { get;set; }

        public async Task OnGetAsync()
        {
            Email = await _context.Email.ToListAsync();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace EmailCollector.Models
{
    public class EmailCollectorContext : DbContext
    {
        public EmailCollectorContext (DbContextOptions<EmailCollectorContext> options)
            : base(options)
        {
        }

        public DbSet<Email> Email { get; set; }
    }
}

using ApiServer.Model;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Data;

public class DataEventRecordContext : DbContext
{
    public DataEventRecordContext(DbContextOptions<DataEventRecordContext> options) : base(options) { }

    public DbSet<DataEventRecord> DataEventRecords => Set<DataEventRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    { 
        builder.Entity<DataEventRecord>().HasKey(m => m.Id); 
        base.OnModelCreating(builder); 
    } 
}
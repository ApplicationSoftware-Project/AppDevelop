using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace App.Features.AI.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            // 기본(localdb) 연결 문자열을 환경변수에서 읽도록 변경했습니다. 개발환경에서는
            // 'DefaultConnection' 환경변수 또는 User Secrets를 설정하시기 바랍니다.
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                             ?? "Server=(localdb)\\mssqllocaldb;Database=ReceiptManager;Trusted_Connection=True;MultipleActiveResultSets=true";
            optionsBuilder.UseSqlServer(connection);
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}

using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations;

[DbContext(typeof(FelDbContext))]
[Migration("20261003190000_AddProductScope")]
partial class AddProductScope
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
    }
}

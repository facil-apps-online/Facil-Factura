using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations;

[DbContext(typeof(FelDbContext))]
[Migration("20261003203000_AddRetentionScope")]
partial class AddRetentionScope
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
    }
}

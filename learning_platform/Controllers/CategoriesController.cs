using DataAccess.Data;
using DataAccess.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/categories")]
    [AllowAnonymous]
    public class CategoriesController(AppDbContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<CategoryDto>>> GetCategories()
        {
            var categories = await context.Categories
                .AsNoTracking()
                .OrderBy(c => c.name)
                .Select(c => new CategoryDto
                {
                    CategoryId = c.category_id,
                    Name = c.name,
                    Slug = c.slug,
                    ParentId = c.parent_id
                })
                .ToListAsync();

            return Ok(categories);
        }
    }
}

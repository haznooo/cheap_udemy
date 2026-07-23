using DataAccess.Data;
using DataAccess.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static DataAccess.Common.clsPageResult;

namespace Api.Controllers
{
    // Lookup list. Deliberately no service/repository layer (documented accepted
    // loose end) — but it DOES inherit ApiControllerBase so it gets the "standard"
    // rate-limit policy and the shared paging conventions like every other controller.
    [ApiController]
    [Route("api/categories")]
    [AllowAnonymous]
    public class CategoriesController(AppDbContext context) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<PageResult<CategoryDto>>> GetCategories(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (pageNumber <= 0 || pageSize <= 0)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Invalid page number or page size.");
            }

            var query = context.Categories.AsNoTracking();

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(c => c.name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CategoryDto
                {
                    CategoryId = c.category_id,
                    Name = c.name,
                    Slug = c.slug,
                    ParentId = c.parent_id
                })
                .ToListAsync();

            return Ok(new PageResult<CategoryDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }
    }
}

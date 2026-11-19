using AbySalto.Mid.Domain.Enums;

namespace AbySalto.Mid.Application.Products.Dtos;

public record CategoryDto(ProductCategory Category, string Slug, string Name);

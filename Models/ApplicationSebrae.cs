namespace ApplicationSebrae.Models;

public class PortfolioViewModel
{
    public int TotalProducts { get; set; }
    public int DossierProducts { get; set; }
    public int NewSolutions { get; set; }
}

public class Product
{
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Modalidade { get; set; } = string.Empty;
    public string CargaHoraria { get; set; } = string.Empty;
    public string UF { get; set; } = string.Empty;
    public string Situacao { get; set; } = string.Empty;
    public string DescricaoResumida { get; set; } = string.Empty;
    public string DescricaoCompleta { get; set; } = string.Empty;
    public string Instrumento { get; set; } = string.Empty;
    public string Macrossegmento { get; set; } = string.Empty;
    public bool RelevanteDossie { get; set; }
    public decimal? Preco { get; set; }
    public List<string> Tags { get; set; } = new();
    public int Visualizacoes { get; set; }
    public double Rating { get; set; }
    public int TotalAvaliacoes { get; set; }
}

// Novo: Sistema de Recomendações


public class ProductReview
{
    public int Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool Verified { get; set; }
}

public class PortfolioAnalytics
{
    public int TotalViews { get; set; }
    public int UniqueVisitors { get; set; }
    public Dictionary<string, int> PopularProducts { get; set; } = new();
    public Dictionary<string, int> SearchTerms { get; set; } = new();
    public List<VisitTrend> DailyVisits { get; set; } = new();
}

public class VisitTrend
{
    public DateTime Date { get; set; }
    public int Visits { get; set; }
}

public class RecommendationEngine
{
    public List<Product> GetSimilarProducts(string productCode, List<Product> allProducts)
    {
        var currentProduct = allProducts.FirstOrDefault(p => p.Codigo == productCode);
        if (currentProduct == null) return new List<Product>();

        return allProducts
            .Where(p => p.Codigo != productCode &&
                       (p.Categoria == currentProduct.Categoria ||
                        p.Macrossegmento == currentProduct.Macrossegmento))
            .Take(4)
            .ToList();
    }      


}
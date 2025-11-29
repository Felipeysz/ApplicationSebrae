using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ApplicationSebrae.Models;
using System.Diagnostics;

namespace ApplicationSebrae.Controllers;

public class PortfolioController : Controller
{
    private readonly ILogger<PortfolioController> _logger;
    private readonly IWebHostEnvironment _environment;
    private static List<ProductReview> _reviews = new();
    private static PortfolioAnalytics _analytics = new();
    private static readonly Dictionary<string, int> _productViews = new();
    private const string ProductsJsonPath = "Data/products.json";
    private const string AdminPassword = "sebrae2024";

    public PortfolioController(ILogger<PortfolioController> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public IActionResult Index()
    {
        // Track visit
        TrackVisit();

        var products = GetAllProducts();
        var model = new PortfolioViewModel
        {
            TotalProducts = products.Count,
            DossierProducts = products.Count(p => p.RelevanteDossie),
            NewSolutions = products.Count(p => IsNewSolution(p.Codigo))
        };

        return View(model);
    }

    // AÇÃO DE LOGIN PARA ADMIN
    public IActionResult AdminLogin()
    {
        return View();
    }

    [HttpPost]
    public IActionResult AdminLogin(string password)
    {
        if (password == AdminPassword)
        {
            HttpContext.Session.SetString("UserRole", "Admin");
            return RedirectToAction("AdminDashboard");
        }

        TempData["Error"] = "Senha incorreta. Tente novamente.";
        return View();
    }

    // DASHBOARD ADMIN
    public IActionResult AdminDashboard()
    {
        if (HttpContext.Session.GetString("UserRole") != "Admin")
        {
            return RedirectToAction("AdminLogin");
        }

        var products = GetAllProducts();
        return View(products);
    }

    // CRIAR NOVO PRODUTO
    public IActionResult CreateProduct()
    {
        if (HttpContext.Session.GetString("UserRole") != "Admin")
        {
            return RedirectToAction("AdminLogin");
        }

        return View(new Product());
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct(Product product)
    {
        if (HttpContext.Session.GetString("UserRole") != "Admin")
        {
            return RedirectToAction("AdminLogin");
        }

        try
        {
            var products = GetAllProducts();

            // Gerar código único se não fornecido
            if (string.IsNullOrEmpty(product.Codigo))
            {
                var lastCode = products.Select(p => p.Codigo)
                    .Where(c => c.StartsWith("CP-"))
                    .Select(c => {
                        if (c.Contains('-') && int.TryParse(c.Split('-')[1], out var num))
                            return num;
                        return 0;
                    })
                    .DefaultIfEmpty(0)
                    .Max();

                product.Codigo = $"CP-{lastCode + 1:D5}";
            }

            // Adicionar produto
            products.Add(product);

            // Salvar no JSON
            await SaveProductsToJson(products);

            TempData["Success"] = "Produto criado com sucesso!";
            return RedirectToAction("AdminDashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar produto");
            TempData["Error"] = "Erro ao criar produto";
            return View(product);
        }
    }

    // EDITAR PRODUTO
    public IActionResult EditProduct(string code)
    {
        if (HttpContext.Session.GetString("UserRole") != "Admin")
        {
            return RedirectToAction("AdminLogin");
        }

        var products = GetAllProducts();
        var product = products.FirstOrDefault(p => p.Codigo == code);

        if (product == null)
        {
            TempData["Error"] = "Produto não encontrado";
            return RedirectToAction("AdminDashboard");
        }

        return View(product);
    }

    [HttpPost]
    public async Task<IActionResult> EditProduct(Product product)
    {
        if (HttpContext.Session.GetString("UserRole") != "Admin")
        {
            return RedirectToAction("AdminLogin");
        }

        try
        {
            var products = GetAllProducts();
            var existingProduct = products.FirstOrDefault(p => p.Codigo == product.Codigo);

            if (existingProduct != null)
            {
                // Atualizar propriedades
                existingProduct.Nome = product.Nome;
                existingProduct.Categoria = product.Categoria;
                existingProduct.Modalidade = product.Modalidade;
                existingProduct.CargaHoraria = product.CargaHoraria;
                existingProduct.UF = product.UF;
                existingProduct.Situacao = product.Situacao;
                existingProduct.DescricaoResumida = product.DescricaoResumida;
                existingProduct.DescricaoCompleta = product.DescricaoCompleta;
                existingProduct.Instrumento = product.Instrumento;
                existingProduct.Macrossegmento = product.Macrossegmento;
                existingProduct.RelevanteDossie = product.RelevanteDossie;
                existingProduct.Preco = product.Preco;
                existingProduct.Tags = product.Tags;
                existingProduct.Visualizacoes = product.Visualizacoes;
                existingProduct.Rating = product.Rating;
                existingProduct.TotalAvaliacoes = product.TotalAvaliacoes;

                // Salvar no JSON
                await SaveProductsToJson(products);

                TempData["Success"] = "Produto atualizado com sucesso!";
            }
            else
            {
                TempData["Error"] = "Produto não encontrado";
            }

            return RedirectToAction("AdminDashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao editar produto");
            TempData["Error"] = "Erro ao editar produto";
            return View(product);
        }
    }

    // EXCLUIR PRODUTO
    [HttpPost]
    public async Task<IActionResult> DeleteProduct(string code)
    {
        if (HttpContext.Session.GetString("UserRole") != "Admin")
        {
            return Json(new { success = false, message = "Não autorizado" });
        }

        try
        {
            var products = GetAllProducts();
            var product = products.FirstOrDefault(p => p.Codigo == code);

            if (product != null)
            {
                products.Remove(product);
                await SaveProductsToJson(products);
                return Json(new { success = true, message = "Produto excluído com sucesso" });
            }

            return Json(new { success = false, message = "Produto não encontrado" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir produto");
            return Json(new { success = false, message = "Erro ao excluir produto" });
        }
    }

    // LOGOUT
    public IActionResult Logout()
    {
        HttpContext.Session.Remove("UserRole");
        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult GetProducts()
    {
        try
        {
            var products = GetAllProducts();
            return Json(new { success = true, products });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar produtos");
            return Json(new { success = false, message = "Erro ao carregar produtos" });
        }
    }

    [HttpGet]
    public IActionResult GetProductDetails(string code)
    {
        try
        {
            var products = GetAllProducts();
            var product = products.FirstOrDefault(p => p.Codigo == code);

            if (product == null)
            {
                return Json(new { success = false, message = "Produto não encontrado" });
            }

            // Incrementar visualizações
            TrackProductView(code);

            // Carregar reviews
            var productReviews = _reviews.Where(r => r.ProductCode == code).ToList();
            product.Rating = productReviews.Any() ? productReviews.Average(r => r.Rating) : 0;
            product.TotalAvaliacoes = productReviews.Count;

            return Json(new { success = true, product, reviews = productReviews });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar detalhes do produto");
            return Json(new { success = false, message = "Erro ao carregar detalhes" });
        }
    }

    [HttpGet]
    public IActionResult GetStatistics()
    {
        var products = GetAllProducts();

        var stats = new
        {
            TotalProducts = products.Count,
            DossierProducts = products.Count(p => p.RelevanteDossie),
            Consultorias = products.Count(p => p.Categoria == "Consultoria"),
            Oficinas = products.Count(p => p.Categoria == "Oficina"),
            Presenciais = products.Count(p => p.Modalidade == "Presencial"),
            NewSolutions = products.Count(p => IsNewSolution(p.Codigo)),
            TotalViews = _analytics.TotalViews,
            UniqueVisitors = _analytics.UniqueVisitors
        };

        return Json(new { success = true, stats });
    }

    [HttpPost]
    public IActionResult AddReview([FromBody] ProductReview review)
    {
        try
        {
            review.Id = _reviews.Count + 1;
            review.CreatedAt = DateTime.Now;
            review.Verified = false;

            _reviews.Add(review);

            return Json(new { success = true, message = "Avaliação adicionada com sucesso!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar avaliação");
            return Json(new { success = false, message = "Erro ao adicionar avaliação" });
        }
    }

    [HttpGet]
    public IActionResult GetSimilarProducts(string code)
    {
        try
        {
            var products = GetAllProducts();
            var similarProducts = GetSimilarProductsInternal(code, products);

            return Json(new { success = true, products = similarProducts });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar produtos similares");
            return Json(new { success = false, message = "Erro ao carregar produtos similares" });
        }
    }

    [HttpGet]
    public IActionResult GetPopularProducts()
    {
        try
        {
            var products = GetAllProducts();
            var popularProducts = products
                .OrderByDescending(p => _productViews.GetValueOrDefault(p.Codigo))
                .ThenByDescending(p => p.Visualizacoes)
                .Take(6)
                .ToList();

            return Json(new { success = true, products = popularProducts });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar produtos populares");
            return Json(new { success = false, message = "Erro ao carregar produtos populares" });
        }
    }

    [HttpGet]
    public IActionResult GetAnalytics()
    {
        return Json(new { success = true, analytics = _analytics });
    }

    [HttpPost]
    public IActionResult TrackSearch([FromBody] SearchTrackingRequest request)
    {
        try
        {
            if (!string.IsNullOrEmpty(request.Term))
            {
                if (_analytics.SearchTerms.ContainsKey(request.Term))
                {
                    _analytics.SearchTerms[request.Term]++;
                }
                else
                {
                    _analytics.SearchTerms[request.Term] = 1;
                }
            }

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao trackear busca");
            return Json(new { success = false });
        }
    }

    private void TrackVisit()
    {
        _analytics.TotalViews++;

        // Simular visitantes únicos (em produção, usar cookies/sessions)
        if (new Random().Next(1, 5) == 1)
        {
            _analytics.UniqueVisitors++;
        }

        // Adicionar tendência diária
        var today = DateTime.Today;
        var todayTrend = _analytics.DailyVisits.FirstOrDefault(t => t.Date == today);

        if (todayTrend != null)
        {
            todayTrend.Visits++;
        }
        else
        {
            _analytics.DailyVisits.Add(new VisitTrend { Date = today, Visits = 1 });

            // Manter apenas últimos 30 dias
            if (_analytics.DailyVisits.Count > 30)
            {
                _analytics.DailyVisits.RemoveAt(0);
            }
        }
    }

    private void TrackProductView(string productCode)
    {
        if (_productViews.ContainsKey(productCode))
        {
            _productViews[productCode]++;
        }
        else
        {
            _productViews[productCode] = 1;
        }

        // Atualizar analytics
        if (_analytics.PopularProducts.ContainsKey(productCode))
        {
            _analytics.PopularProducts[productCode]++;
        }
        else
        {
            _analytics.PopularProducts[productCode] = 1;
        }
    }

    private List<Product> GetAllProducts()
    {
        try
        {
            var jsonPath = Path.Combine(_environment.WebRootPath, ProductsJsonPath);

            // Se não encontrar no wwwroot, tenta no ContentRoot
            if (!System.IO.File.Exists(jsonPath))
            {
                jsonPath = Path.Combine(_environment.ContentRootPath, ProductsJsonPath);
            }

            if (!System.IO.File.Exists(jsonPath))
            {
                _logger.LogWarning("Arquivo JSON não encontrado, retornando lista vazia");
                return new List<Product>();
            }

            var jsonContent = System.IO.File.ReadAllText(jsonPath);
            var productsData = JsonSerializer.Deserialize<ProductsData>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return productsData?.Products ?? new List<Product>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar produtos do JSON");
            return new List<Product>();
        }
    }

    private async Task SaveProductsToJson(List<Product> products)
    {
        try
        {
            var jsonPath = Path.Combine(_environment.WebRootPath, ProductsJsonPath);
            var directory = Path.GetDirectoryName(jsonPath);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            var productsData = new ProductsData { Products = products };
            var jsonContent = JsonSerializer.Serialize(productsData, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await System.IO.File.WriteAllTextAsync(jsonPath, jsonContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar produtos no JSON");
            throw;
        }
    }

    private bool IsNewSolution(string codigo)
    {
        var newSolutionsCodes = new[] { "CP-13330", "CP-11972", "CP-04490", "CP-05795", "CP-19608", "CP-12861", "CP-13923", "CP-02552" };
        return newSolutionsCodes.Contains(codigo);
    }

    private List<Product> GetSimilarProductsInternal(string code, List<Product> products, int count = 3)
    {
        var currentProduct = products.FirstOrDefault(p => p.Codigo == code);
        if (currentProduct == null) return new List<Product>();

        // Encontrar produtos similares baseados na categoria e tags
        var similarProducts = products
            .Where(p => p.Codigo != code)
            .OrderByDescending(p =>
                (p.Categoria == currentProduct.Categoria ? 1 : 0) +
                (p.Macrossegmento == currentProduct.Macrossegmento ? 1 : 0) +
                (p.Tags?.Intersect(currentProduct.Tags ?? new List<string>()).Count() ?? 0)
            )
            .Take(count)
            .ToList();

        return similarProducts;
    }
}

// Classes auxiliares para o JSON
public class ProductsData
{
    public List<Product> Products { get; set; } = new();
}

public class SearchTrackingRequest
{
    public string Term { get; set; } = string.Empty;
}

// Classe de analytics
public class PortfolioAnalytics
{
    public int TotalViews { get; set; }
    public int UniqueVisitors { get; set; }
    public Dictionary<string, int> SearchTerms { get; set; } = new();
    public Dictionary<string, int> PopularProducts { get; set; } = new();
    public List<VisitTrend> DailyVisits { get; set; } = new();
}

public class VisitTrend
{
    public DateTime Date { get; set; }
    public int Visits { get; set; }
}

// Classe para reviews
public class ProductReview
{
    public int Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool Verified { get; set; }
}
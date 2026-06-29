using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;

public class OmiePlugin
{
    private readonly HttpClient _http;
    private readonly string _appKey;
    private readonly string _appSecret;
    private const string BaseUrl = "https://app.omie.com.br/api/v1";

    public OmiePlugin(HttpClient http, string appKey, string appSecret)
    {
        _http = http;
        _appKey = appKey;
        _appSecret = appSecret;
    }

    // Helper: monta o body padrão do Omie
    private StringContent MontarBody(string call, object param)
    {
        var payload = new
        {
            call,
            app_key = _appKey,
            app_secret = _appSecret,
            param = new[] { param }
        };
        var json = JsonSerializer.Serialize(payload);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }


    // ---------------------------------------------------------------
    // TOOL 3: Criar Ordem de Serviço
    // ---------------------------------------------------------------
    [KernelFunction("criar_ordem_servico")]
    [Description("Cria uma Ordem de Serviço (OS) no Omie para um cliente. Retorna o código da OS criada (nCodOS).")]
    public async Task<string> CriarOrdemServico(
        [Description("Código interno do cliente no Omie (nCodCli)")] long nCodCli,
        [Description("Descrição do serviço prestado")] string descricaoServico,
        [Description("Valor total do serviço em reais")] decimal valor)
    {
        var body = MontarBody("IncluirOS", new
        {
            cabecalho = new
            {
                nCodCli,
                dDtPrevisao = DateTime.Now.ToString("dd/MM/yyyy"),
                cEtapa = "10" // etapa inicial padrão
            },
            servicos = new[]
            {
                new
                {
                    cDescServ = descricaoServico,
                    nQtde = 1,
                    nValUnit = valor
                }
            },
            informacoes_adicionais = new
            {
                cDadosAdicNF = descricaoServico
            }
        });

        var response = await _http.PostAsync($"{BaseUrl}/servicos/os/", body);
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);

        if (doc.RootElement.TryGetProperty("nCodOS", out var codOS))
        {
            return $"Ordem de Serviço criada! nCodOS: {codOS.GetInt64()}";
        }

        if (doc.RootElement.TryGetProperty("faultstring", out var erro))
            return $"Erro ao criar OS: {erro.GetString()}";

        return $"Resposta inesperada: {content}";
    }

    // ---------------------------------------------------------------
    // TOOL 4: Faturar OS (emite a NFS-e)
    // ---------------------------------------------------------------
    [KernelFunction("faturar_ordem_servico")]
    [Description("Fatura uma Ordem de Serviço no Omie, gerando a NFS-e junto à prefeitura.")]
    public async Task<string> FaturarOrdemServico(
        [Description("Código da Ordem de Serviço no Omie (nCodOS)")] long nCodOS)
    {
        var body = MontarBody("FaturarOS", new
        {
            nCodOS,
            nCodCC = 0,      // conta corrente (0 = padrão)
            dDtFatur = DateTime.Now.ToString("dd/MM/yyyy")
        });

        var response = await _http.PostAsync($"{BaseUrl}/servicos/os/", body);
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);

        if (doc.RootElement.TryGetProperty("cSitNFSe", out var sit))
        {
            return $"OS faturada! Situação NFS-e: {sit.GetString()}";
        }

        if (doc.RootElement.TryGetProperty("faultstring", out var erro))
            return $"Erro ao faturar OS: {erro.GetString()}";

        return $"Resposta da Omie: {content}";
    }
}
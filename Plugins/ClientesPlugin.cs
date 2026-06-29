using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace LGN_AGENTE.Plugins
{
    public class ClientesPlugin
    {
        private readonly HttpClient _http;
        private readonly string _appKey;
        private readonly string _appSecret;
        private const string BaseUrl = "https://app.omie.com.br/api/v1";

        public ClientesPlugin(HttpClient http, string appKey, string appSecret)
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

        /// <summary>
        /// Metodo resposável por cadastrar um novo cliente no Omie. Retorna o código do cliente criado (nCodCli).
        /// </summary>
        /// <param name="nome"></param>
        /// <param name="cpfCnpj"></param>
        /// <returns></returns>
        [KernelFunction("cadastrar_cliente")]
        [Description("Cadastra um novo cliente no Omie. Retorna o código do cliente criado (nCodCli).")]
        public async Task<string> CadastrarCliente(
          [Description("Nome completo ou razão social do cliente")] string nome,
          [Description("CPF ou CNPJ do cliente (somente números)")] string cpfCnpj)
        {
            var body = MontarBody("IncluirCliente", new
            {
                nome_fantasia = nome,
                razao_social = nome,
                cnpj_cpf = cpfCnpj,
                pessoa_fisica = cpfCnpj.Length <= 11 ? "S" : "N",
                inativo = "N"
            });

            var response = await _http.PostAsync($"{BaseUrl}/geral/clientes/", body);
            var content = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("codigo_cliente_omie", out var cod))
            {
                return $"Cliente cadastrado com sucesso! nCodCli: {cod.GetInt64()}";
            }

            // Retorna o erro do Omie se houver
            if (doc.RootElement.TryGetProperty("faultstring", out var erro))
            {
                return $"Erro ao cadastrar cliente: {erro.GetString()}";
            }

            return $"Resposta inesperada: {content}";
        }

        /// <summary>
        /// Metodo responsável por buscar um cliente no Omie pelo nome. Retorna o código do cliente (nCodCli) se encontrado, ou vazio se não encontrado.
        /// </summary>
        /// <param name="nome"></param>
        /// <returns></returns>
        [KernelFunction("buscar_cliente")]
        [Description("Busca um cliente no Omie pelo nome. Retorna o código do cliente (nCodCli) se encontrado, ou vazio se não encontrado.")]
        public async Task<string> BuscarCliente(
          [Description("Nome do cliente a buscar")] string nome)
        {
            var body = MontarBody("ListarClientes", new
            {
                pagina = 1,
                registros_por_pagina = 5,
                apenas_importado_api = "N",
                filtrar_por_nome = nome
            });

            var response = await _http.PostAsync($"{BaseUrl}/geral/clientes/", body);
            var content = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(content);

            // Verifica se veio a lista de clientes
            if (doc.RootElement.TryGetProperty("clientes_cadastro", out var lista) && lista.GetArrayLength() > 0)
            {
                var cliente = lista[0];
                var codCli = cliente.GetProperty("codigo_cliente_omie").GetInt64();
                var nomeCliente = cliente.GetProperty("nome_fantasia").GetString();
                return $"Cliente encontrado: {nomeCliente} | nCodCli: {codCli}";
            }

            return "Cliente não encontrado no sistema.";
        }




    }
}

using LGN_AGENTE.Services.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;

namespace LGN_AGENTE.Services.Agente
{
    public class AgenteService
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chat;

        // Histórico mantido entre mensagens para o agente lembrar o contexto
        private readonly ChatHistory _historico;

        public AgenteService(IHttpClientFactory httpFactory, IConfiguration config)
        {
            var builder = Kernel.CreateBuilder();

            builder.AddOllamaChatCompletion(
                modelId: "qwen3:4b",
                endpoint: new Uri("http://localhost:11434"));

            // Registra o plugin do Omie (igual ao ViaCepPlugin do seu exemplo)
            var appKey = config["Omie:AppKey"]!;
            var appSecret = config["Omie:AppSecret"]!;

            builder.Plugins.AddFromObject(
                new OmiePlugin(httpFactory.CreateClient(), appKey, appSecret));

            _kernel = builder.Build();
            _chat = _kernel.GetRequiredService<IChatCompletionService>();

            // Histórico começa com o system prompt e fica vivo enquanto o AgentService existir
            _historico = new ChatHistory();
            _historico.AddSystemMessage(
                "Você é um assistente de escritório de contabilidade integrado ao Omie ERP. " +
                "Quando o usuário pedir para emitir uma nota fiscal de serviço, siga este fluxo: " +
                "1) Busque o cliente pelo nome usando buscar_cliente. " +
                "2) Se não encontrar, pergunte ao usuário se deseja cadastrar e peça o CPF/CNPJ. " +
                "3) Se confirmar, cadastre usando cadastrar_cliente. " +
                "4) Crie a Ordem de Serviço usando criar_ordem_servico. " +
                "5) Fature usando faturar_ordem_servico para gerar a NFS-e. " +
                "Sempre confirme com o usuário antes de faturar. " +
                "Responda sempre em português.");
        }

        public async Task<string> ProcessarMensagem(string mensagem)
        {
            _historico.AddUserMessage(mensagem);

            // Igual ao seu exemplo do CEP, mas com a versão Ollama das settings
            // FunctionChoiceBehavior.Auto() = agente decide sozinho quando chamar as tools
            var settings = new OllamaPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var resultado = await _chat.GetChatMessageContentAsync(
                _historico,
                settings,
                _kernel);

            // Adiciona a resposta do agente no histórico para ele lembrar na próxima mensagem
            _historico.AddAssistantMessage(resultado.Content ?? "");

            return resultado.Content ?? "Sem resposta.";
        }

        // Limpa o histórico para começar uma nova conversa
        public void LimparHistorico()
        {
            _historico.Clear();
            _historico.AddSystemMessage(
                "Você é um assistente de escritório de contabilidade integrado ao Omie ERP...");
        }
    }
}

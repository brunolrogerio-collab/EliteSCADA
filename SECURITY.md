# Security Policy

## Segredos e credenciais

Nunca versionar no repositório:

- senhas de usuários ou serviços;
- tokens de API;
- strings de conexão com senha embutida;
- chaves privadas;
- certificados com chave privada;
- credenciais de PLC, gateways, bancos de dados ou servidores;
- arquivos `.env` reais;
- `secrets.json`;
- arquivos locais de configuração contendo dados sensíveis.

Use variáveis de ambiente, GitHub Actions Secrets ou outro cofre de segredos apropriado.

## Engineering Import/Export

Arquivos exportados de engenharia não devem conter segredos em texto aberto. Data Sources e Drivers devem referenciar segredos por identificadores protegidos, nunca serializar credenciais diretamente.

## Incidente

Se um segredo for versionado acidentalmente, considere-o comprometido: remova-o do repositório e faça a rotação/revogação da credencial imediatamente.

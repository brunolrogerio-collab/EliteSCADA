# EliteSCADA

Plataforma SCADA / Supervisório Industrial.

## Princípios do projeto

- arquitetura modular e evolutiva;
- entidades de engenharia serializáveis e versionáveis;
- Engineering Import/Export como recurso transversal desde as fases iniciais;
- segredos e credenciais nunca armazenados em texto aberto nos arquivos de engenharia;
- testes automatizados e integração contínua desde o início.

## Engineering Import/Export

O projeto deverá permitir importação e exportação pública, sem dependência da interface gráfica, das principais entidades de engenharia, incluindo:

- TAGs;
- alarmes;
- Data Sources e Drivers;
- equipamentos;
- templates e dínamos;
- telas e popups;
- bindings, propriedades e metadados relacionados.

Configurações técnicas podem ser exportadas, mas credenciais, senhas, tokens e chaves devem ser referenciados por mecanismos seguros separados.

## CI

O workflow `.github/workflows/dotnet-ci.yml` está preparado para executar automaticamente restore, build e testes quando o código .NET for adicionado ao repositório.

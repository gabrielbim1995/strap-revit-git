# 🏗️ STRAP-REVIT Plugin

## 📋 Descrição
Plugin avançado para Autodesk Revit 2024 que permite importar arquivos IFC exportados pelo STRAP e criar elementos estruturais nativos e paramétricos no Revit, sem usar DirectShapes.

## ✨ Funcionalidades Principais

### 🎯 Importação Inteligente de IFC
- **Elementos Nativos**: Cria elementos reais do Revit, não DirectShapes
  - Vigas → Structural Framing
  - Pilares → Structural Columns  
  - Lajes → Floors
  - Fundações → Structural Foundations

### 🔄 Sincronização Incremental
- **Atualização Inteligente**: Baseada em GUIDs IFC persistidos
- **Detecção de Mudanças**: Atualiza apenas o que foi modificado
- **Elementos Órfãos**: Marca elementos que não existem mais no IFC

### 🏗️ Mapeamento Avançado
- **Perfis Estruturais**: Retangular, Circular, I, T, L, Custom
- **Materiais**: Criação automática quando não existirem
- **Níveis**: Criação automática com elevações corretas
- **Dimensões**: Conversão precisa de unidades (mm → ft)

### 📊 Rastreabilidade Completa
- **Parâmetros IFC**: GUID, Classe, Arquivo origem, Timestamp
- **Histórico**: Data/hora de cada sincronização
- **Auditoria**: Rastreamento completo de criações e atualizações

## 🚀 Como Usar

### Instalação
1. Compile o projeto: `dotnet build STRAP-REVIT.csproj --configuration Release`
2. Copie para pasta do Revit:
   - `bin\Release\StrapRevit.dll`
   - `STRAP-REVIT.addin`
3. Reinicie o Revit

### Importação de IFC
1. Abra um projeto no Revit
2. Vá para aba **"STRAP-REVIT"** no Ribbon
3. Clique em **"Importar IFC STRAP"**
4. Selecione o arquivo IFC exportado pelo STRAP
5. Configure opções:
   - ✅ Criar níveis automaticamente
   - ✅ Criar materiais automaticamente
   - ✅ Atualizar elementos existentes
6. Clique em OK e aguarde o processamento

### Re-sincronização
1. Exporte novo IFC do STRAP com as alterações
2. Execute novamente "Importar IFC STRAP"
3. O plugin irá:
   - Criar novos elementos
   - Atualizar elementos modificados
   - Marcar elementos órfãos
   - Manter elementos inalterados

## 🏗️ Arquitetura do Plugin

```
📁 STRAP-REVIT/
├── 🎯 Commands/
│   └── ImportIFCCommand.cs         # Comando principal de importação
├── ⚙️ Core/
│   ├── IFCProcessor.cs            # Processador principal
│   ├── IFC/
│   │   ├── IFCParser.cs          # Parser de arquivos IFC
│   │   └── IFCModel.cs           # Modelos de dados IFC
│   ├── Mapping/
│   │   ├── ElementMapper.cs      # Mapeamento IFC → Revit
│   │   ├── FamilyLoader.cs       # Carregamento de famílias
│   │   ├── LevelCreator.cs       # Criação de níveis
│   │   └── MaterialCreator.cs    # Criação de materiais
│   └── Parameters/
│       └── ParameterManager.cs    # Gerenciamento de parâmetros
├── 🖼️ Views/
│   ├── ImportConfigurationForm.cs  # Configurações de importação
│   └── ImportProgressForm.cs       # Progresso da importação
└── 📄 Application.cs              # Plugin principal
```

## 🔧 Detalhes Técnicos

### Parser IFC
- Leitura completa de entidades IFC em memória
- Extração de: IfcBeam, IfcColumn, IfcSlab, IfcFooting
- Parsing de atributos, relações e propriedades
- Normalização de unidades para milímetros

### Mapeamento de Elementos
- **Vigas**: Perfis I, T, L, retangular, circular
- **Pilares**: Seções retangulares e circulares
- **Lajes**: Contornos complexos com espessura
- **Fundações**: Sapatas isoladas, corridas e radier

### Criação de Tipos
- Busca tipos existentes compatíveis
- Cria novos tipos com dimensões corretas
- Carrega famílias padrão quando necessário
- Aplica materiais e parâmetros

### Parâmetros de Rastreabilidade
- `IFC_GUID`: Identificador único do elemento
- `IFC_Class`: Classe IFC original
- `IFC_SourceFile`: Arquivo de origem
- `IFC_LastSync`: Data/hora da última sincronização
- `IFC_Orphan`: Marca elementos órfãos
- `IFC_TypeName`: Nome do tipo no IFC
- `IFC_Material`: Material do IFC

## 📊 Relatório de Importação

Após cada importação, o plugin mostra:
- Total de elementos processados
- Elementos criados vs atualizados
- Elementos órfãos detectados
- Materiais e níveis criados
- Avisos e erros encontrados
- Tempo total de processamento

## ⚡ Performance

- **Parsing IFC**: ~1-2 segundos por MB
- **Criação de elementos**: ~100 elementos/segundo
- **Atualização**: Apenas elementos modificados
- **Memória**: Otimizado para arquivos grandes

## 🛡️ Robustez

- **Transações agrupadas**: Rollback em caso de erro
- **Validações**: Verifica dados antes de criar
- **Fallbacks**: Usa valores padrão quando necessário
- **Logs detalhados**: Para debugging e auditoria

## 🎯 Casos de Uso

### Fluxo STRAP → Revit
1. Modelagem estrutural no STRAP
2. Exportação para IFC
3. Importação no Revit via plugin
4. Elementos nativos prontos para documentação

### Atualização de Projeto
1. Alterações no modelo STRAP
2. Nova exportação IFC
3. Re-importação incremental
4. Apenas alterações são aplicadas

### Colaboração BIM
1. Modelo estrutural preciso do STRAP
2. Elementos nativos no Revit
3. Compatível com outros softwares BIM
4. Rastreabilidade completa

## 📋 Limitações Conhecidas

- Requer Revit 2021-2025
- IFC deve ser exportado com GUIDs consistentes
- Perfis customizados complexos podem usar fallback
- Materiais compostos são simplificados

## 🚀 Roadmap Futuro

- [ ] Suporte para mais tipos de elementos
- [ ] Importação de armaduras
- [ ] Configurações de mapeamento customizáveis
- [ ] Exportação de relatórios detalhados
- [ ] Interface para gerenciar elementos órfãos

---

**Plugin STRAP-REVIT v1.0.0**  
**STRAP Engineering Solutions © 2025**  
**Integração profissional STRAP ↔ Revit via IFC** 🏗️
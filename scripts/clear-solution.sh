#!/bin/bash

# Cores para output (opcional, para melhor legibilidade)
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Log com timestamp
log() {
    echo -e "${BLUE}[$(date '+%Y-%m-%d %H:%M:%S')]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[$(date '+%Y-%m-%d %H:%M:%S')] ?${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[$(date '+%Y-%m-%d %H:%M:%S')] ?${NC} $1"
}

log_error() {
    echo -e "${RED}[$(date '+%Y-%m-%d %H:%M:%S')] ?${NC} $1"
}

# Função para procurar por arquivo .sln recursivamente nas pastas pai
find_sln_file() {
    local current_dir="$1"
    local depth=0
    
    log "Iniciando busca por arquivo *.sln a partir de: $current_dir"
    
    while [[ "$current_dir" != "/" ]]; do
        log "  [Nível $depth] Procurando em: $current_dir"
        
        # Procura por arquivo .sln na pasta atual
        local sln_file=$(find "$current_dir" -maxdepth 1 -name "*.sln" -type f | head -1)
        
        if [[ -n "$sln_file" ]]; then
            log_success "Arquivo .sln encontrado após $depth nível(is) de subida: $sln_file"
            echo "$sln_file"
            return 0
        fi
        
        # Sobe para a pasta pai
        current_dir=$(dirname "$current_dir")
        ((depth++))
    done
    
    return 1
}

log "=========================================="
log "Iniciando limpeza da solução .NET"
log "=========================================="

# Inicia a busca na pasta atual
current_path="$(pwd)"
log "Pasta de trabalho atual: $current_path"

sln_path=$(find_sln_file "$current_path")

if [[ -z "$sln_path" ]]; then
    log_error "Nenhum arquivo *.sln encontrado!"
    exit 1
fi

log_success "Arquivo .sln localizado: $sln_path"

# Obtém o diretório do arquivo .sln
sln_dir=$(dirname "$sln_path")
log "Diretório raiz da solução: $sln_dir"

log "=========================================="
log "Procurando por pastas 'bin' e 'obj'"
log "=========================================="

# Conta as pastas que serão removidas
bin_count=$(find "$sln_dir" -type d -name "bin" 2>/dev/null | wc -l)
obj_count=$(find "$sln_dir" -type d -name "obj" 2>/dev/null | wc -l)
total_count=$((bin_count + obj_count))

log "Pastas 'bin' encontradas: $bin_count"
log "Pastas 'obj' encontradas: $obj_count"
log "Total de pastas para remover: $total_count"

if [[ $total_count -eq 0 ]]; then
    log_warning "Nenhuma pasta 'bin' ou 'obj' encontrada para remover"
else
    log "Listando pastas que serão removidas:"
    find "$sln_dir" -type d \( -name "bin" -o -name "obj" \) 2>/dev/null | while read -r folder; do
        log "  ? $folder"
    done
    
    log "=========================================="
    log "Iniciando remoção de pastas..."
    log "=========================================="
    
    # Procura e exclui as pastas bin e obj recursivamente
    removed_count=0
    find "$sln_dir" -type d \( -name "bin" -o -name "obj" \) -print0 2>/dev/null | while IFS= read -r -d '' folder; do
        if rm -rf "$folder" 2>/dev/null; then
            log_success "Removida: $folder"
            ((removed_count++))
        else
            log_error "Erro ao remover: $folder"
        fi
    done
fi

log "=========================================="
log_success "Limpeza concluída com sucesso!"
log "=========================================="

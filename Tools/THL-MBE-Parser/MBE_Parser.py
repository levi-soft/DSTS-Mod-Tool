import os
import sys
import struct
import csv

# --- Definições de Colunas ---
COLUMN_TYPES = {
    0x2: {'name': 'Int',      'size': 4, 'format': '<i'},
    0x9: {'name': 'IntID',    'size': 4, 'format': '<i'},
    0x4: {'name': 'byte',     'size': 1, 'format': '<b'},
    0x5: {'name': 'float',    'size': 4, 'format': '<f'},
    0x7: {'name': 'String',   'size': 8, 'format': '<q'},
    0x8: {'name': 'StringID', 'size': 8, 'format': '<q'},
}

# --- Funções Auxiliares ---

def read_string_with_padding(f, size):
    """Lê uma string de um tamanho fixo e remove o padding nulo."""
    if size <= 0:
        return ""
    raw_bytes = f.read(size)
    return raw_bytes.decode('utf-8', errors='ignore').rstrip('\x00')

def align_offset(offset, alignment):
    """Calcula o próximo offset que é múltiplo do alinhamento requerido."""
    return (offset + alignment - 1) & ~(alignment - 1)

# --- Lógica Principal ---

def parse_mbe(filepath):
    """
    Analisa (parse) um arquivo .MBE, respeitando todas as regras de padding e alinhamento.
    """
    try:
        with open(filepath, 'rb') as f:
            # 1. Header Principal
            magic_number = f.read(4)
            if magic_number != b'EXPA':
                raise ValueError("Magic Number 'EXPA' não encontrado. O arquivo não é um MBE válido.")
            
            num_expa_regions, = struct.unpack('<i', f.read(4))
            print(f"Arquivo MBE detectado. Encontradas {num_expa_regions} planilhas (Regiões EXPA).")

            all_sheets_data = []
            string_location_map = {}

            # 2. Loop pelas Regiões EXPA (Planilhas)
            for sheet_idx in range(num_expa_regions):
                print(f"\n--- Analisando Planilha {sheet_idx+1}/{num_expa_regions} ---")
                
                # 2.1 Header da Região EXPA
                sheet_name_size, = struct.unpack('<i', f.read(4))
                sheet_name = read_string_with_padding(f, sheet_name_size)
                
                num_columns, = struct.unpack('<i', f.read(4))
                column_type_codes = struct.unpack(f'<{num_columns}i', f.read(4 * num_columns))
                columns = [COLUMN_TYPES[code] for code in column_type_codes]
                column_headers = [f"{col['name']}_{idx}" for idx, col in enumerate(columns)]
                
                expa_area_size, = struct.unpack('<i', f.read(4))
                num_expa_areas, = struct.unpack('<i', f.read(4))

                print(f"Nome da Planilha: '{sheet_name}'")
                print(f"Colunas: {num_columns}, Linhas: {num_expa_areas}, Tamanho da Linha: {expa_area_size} bytes")

                # 2.2 Áreas EXPA (Linhas)
                sheet_rows = []
                for row_idx in range(num_expa_areas):
                    # **** REGRA DE PADDING CORRIGIDA ****
                    # Garante que CADA Área EXPA (linha) comece em um offset divisível por 8.
                    # Este padding (geralmente bytes 0xCC) ocorre ENTRE as linhas.
                    current_pos = f.tell()
                    padding_needed = (8 - (current_pos % 8)) % 8
                    if padding_needed > 0:
                        f.read(padding_needed) # Descarta os bytes de padding

                    row_start_offset_in_file = f.tell()
                    row_data_bytes = f.read(expa_area_size)
                    
                    parsed_row = []
                    current_offset_in_row = 0

                    for col_idx, col_info in enumerate(columns):
                        alignment = 8 if col_info['size'] == 8 else 4
                        if col_info['size'] == 1: alignment = 1
                        
                        current_offset_in_row = align_offset(current_offset_in_row, alignment)
                        
                        if col_info['name'] in ('String', 'StringID'):
                            absolute_file_offset = row_start_offset_in_file + current_offset_in_row
                            string_location_map[absolute_file_offset] = (sheet_idx, row_idx, col_idx)
                            parsed_row.append('') # Placeholder
                        else:
                            value_bytes = row_data_bytes[current_offset_in_row : current_offset_in_row + col_info['size']]
                            value, = struct.unpack(col_info['format'], value_bytes)
                            parsed_row.append(value)
                        
                        current_offset_in_row += col_info['size']
                    
                    sheet_rows.append(parsed_row)
                
                all_sheets_data.append({
                    'name': sheet_name, 'columns': columns,
                    'headers': column_headers, 'rows': sheet_rows
                })

            # Alinhamento para a Seção CHNK (esta parte estava correta)
            current_pos = f.tell()
            padding_needed = (8 - (current_pos % 8)) % 8
            if padding_needed > 0:
                print(f"\nAlinhando Seção CHNK em 8 bytes. Pulando {padding_needed} bytes de padding.")
                f.read(padding_needed)

            # 3. Seção CHNK (Strings)
            chnk_magic = f.read(4)
            if chnk_magic != b'CHNK':
                print("\nAviso: Seção 'CHNK' não encontrada após o fim das Regiões EXPA.")
            else:
                num_strings, = struct.unpack('<i', f.read(4))
                print(f"\n--- Analisando Seção CHNK e preenchendo strings ---")
                print(f"Encontradas {num_strings} strings para processar.")
                
                for _ in range(num_strings):
                    symbolic_offset, = struct.unpack('<i', f.read(4))
                    string_size, = struct.unpack('<i', f.read(4))
                    string_value = read_string_with_padding(f, string_size)
                    
                    location = string_location_map.get(symbolic_offset)
                    if location:
                        sheet_idx, row_idx, col_idx = location
                        all_sheets_data[sheet_idx]['rows'][row_idx][col_idx] = string_value
                    else:
                        print(f"Aviso: A string '{string_value}' com offset simbólico {symbolic_offset} não corresponde a nenhuma localização mapeada na seção EXPA.")

            return all_sheets_data

    except FileNotFoundError:
        print(f"Erro: O arquivo '{filepath}' não foi encontrado.")
        return None
    except Exception as e:
        print(f"Ocorreu um erro inesperado durante o parsing: {e}")
        import traceback
        traceback.print_exc()
        return None

def create_csv_files(output_dir, sheets_data):
    if not sheets_data:
        print("Nenhum dado para gerar CSVs.")
        return

    if not os.path.exists(output_dir):
        os.makedirs(output_dir)
        print(f"\nDiretório '{output_dir}' criado.")

    print("\n--- Gerando arquivos CSV ---")
    # **** REGRA DE NOMENCLATURA CORRIGIDA ****
    # Itera usando enumerate para obter o índice da planilha
    for idx, sheet in enumerate(sheets_data):
        safe_sheet_name = "".join(c for c in sheet['name'] if c.isalnum() or c in (' ', '_', '-')).rstrip()
        # Adiciona o prefixo numérico ao nome do arquivo
        csv_filename = f"{idx}_{safe_sheet_name}.csv"
        csv_filepath = os.path.join(output_dir, csv_filename)

        try:
            with open(csv_filepath, 'w', newline='', encoding='utf-8-sig') as csvfile:
                writer = csv.writer(csvfile)
                writer.writerow(sheet['headers'])
                writer.writerows(sheet['rows'])
            
            print(f"Arquivo '{csv_filepath}' gerado com sucesso.")

        except IOError as e:
            print(f"Erro ao escrever o arquivo CSV '{csv_filepath}': {e}")

def main():
    if len(sys.argv) != 2:
        print("Uso: python parser_mbe.py <caminho_para_o_arquivo.mbe>")
        sys.exit(1)

    input_filepath = sys.argv[1]
    output_dir = os.path.splitext(os.path.basename(input_filepath))[0]

    print(f"Processando arquivo: {input_filepath}")
    
    sheets_data = parse_mbe(input_filepath)

    if sheets_data:
        create_csv_files(output_dir, sheets_data)
        print("\nProcessamento concluído!")

if __name__ == "__main__":
    main()
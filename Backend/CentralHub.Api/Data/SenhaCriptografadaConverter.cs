using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CentralHub.Api.Data;

/// <summary>
/// Criptografa a senha da central ao gravar no banco e descriptografa ao ler,
/// de forma transparente para os services. Valores gravados antes da
/// criptografia (sem o prefixo) são lidos como texto simples e passam a ser
/// gravados criptografados na próxima vez que a central for salva.
/// </summary>
public class SenhaCriptografadaConverter : ValueConverter<string, string>
{
    private const string Prefixo = "enc:v1:";

    public SenhaCriptografadaConverter(IDataProtector protector)
        : base(
            valor => Prefixo + protector.Protect(valor),
            armazenado => armazenado.StartsWith(Prefixo, StringComparison.Ordinal)
                ? protector.Unprotect(armazenado.Substring(Prefixo.Length))
                : armazenado)
    {
    }
}

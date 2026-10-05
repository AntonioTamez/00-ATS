# 🐧 Curso Linux Básico para DevOps
### Por Jean Reyes (@jemjaf)

¡Bienvenido al curso! Aquí aprenderás los fundamentos de Linux que necesitas para DevOps y Cloud Computing.

---

## 📚 Índice del Curso

### Módulo 0: Setup del Entorno (Opcional)
| Clase | Tema | Duración |
|-------|------|----------|
| 0.1 | Instalar WSL en Windows | 5 min |
| 0.2 | Crear VM en AWS | 5-7 min |
| 0.3 | Conectarse por SSH desde PowerShell | 5 min |

> 💡 Si ya tienes WSL o una VM Linux, puedes saltar este módulo.

---

### Módulo 1: Navegación Básica ✅
| Clase | Tema | Duración |
|-------|------|----------|
| 1.1 | Primeros pasos en la terminal | 5 min |
| 1.2 | Ver contenido de archivos | 5 min |
| 1.3 | Crear, copiar, mover y eliminar | 5 min |

---

### Módulo 2: Procesamiento de Texto ✅
| Clase | Tema | Duración |
|-------|------|----------|
| 2.1 | Heredocs - Crear archivos multilínea | 5 min |
| 2.2 | grep - Búsqueda de patrones | 5 min |
| 2.3 | awk - Procesamiento de columnas | 5 min |
| 2.4 | sed - Editor de flujos | 5 min |
| 2.5 | stdin, stdout, stderr | 5 min |

---

### Módulo 3: Variables y Alias
| Clase | Tema | Duración |
|-------|------|----------|
| 3.1 | Variables de entorno | 5 min |
| 3.2 | Alias - Atajos para comandos | 5 min |

---

### Módulo 4: Usuarios y Permisos
| Clase | Tema | Duración |
|-------|------|----------|
| 4.1 | Gestión de usuarios y grupos | 5 min |
| 4.2 | Permisos de archivos (chmod, chown) | 5 min |
| 4.3 | Sudo y permisos especiales | 5 min |

---

### Módulo 5: Bash Scripting Básico
| Clase | Tema | Duración |
|-------|------|----------|
| 5.1 | Tu primer script | 5 min |
| 5.2 | Permisos de ejecución (+x) | 5 min |
| 5.3 | Ejecutar con y sin bash | 5 min |

---

### Módulo 6: Automatización con Cron
| Clase | Tema | Duración |
|-------|------|----------|
| 6.1 | Introducción a Cron | 5 min |
| 6.2 | Mejores prácticas y debugging | 5 min |

---

### Módulo 7: SSH Completo
| Clase | Tema | Duración |
|-------|------|----------|
| 7.1 | SSH básico | 5 min |
| 7.2 | SSH con llaves (keys) | 5-7 min |
| 7.3 | Port Forwarding (túneles) | 5 min |
| 7.4 | Copiar archivos (scp, rsync) | 5 min |
| 7.5 | PuTTY - Contexto histórico | 3-5 min |

---

### Módulo 8: Diagnóstico de Red
| Clase | Tema | Duración |
|-------|------|----------|
| 8.1 | Diagnóstico de conectividad (curl, ping, nc) | 5 min |
| 8.2 | DNS con dig | 5 min |

---

### Módulo 9: Gestión de Paquetes
| Clase | Tema | Duración |
|-------|------|----------|
| 9.1 | APT - Debian/Ubuntu | 5 min |
| 9.2 | YUM/DNF - Amazon Linux/RHEL | 5 min |

---

### Módulo 10: Compresión y Archivado
| Clase | Tema | Duración |
|-------|------|----------|
| 10.1 | tar, gzip, zip | 5 min |

---

### 🔷 Módulo 11: JSON y YAML (Avanzado)
| Clase | Tema | Duración |
|-------|------|----------|
| 11.1 | jq Básico | 5 min |
| 11.2 | jq Avanzado | 5 min |
| 11.3 | yq - YAML | 5 min |

---

### 🔷 Módulo 12: Gestión de Procesos (Avanzado)
| Clase | Tema | Duración |
|-------|------|----------|
| 12.1 | top, htop, ps, kill | 5-7 min |
| 12.2 | watch, time y sleep | 8-10 min |

---

### 🔷 Módulo 13: Gestión de Servicios (Avanzado)
| Clase | Tema | Duración |
|-------|------|----------|
| 13.1 | Gestión de servicios con Nginx | 5-7 min |

---

## 📊 Resumen del Curso

| Métrica | Valor |
|---------|-------|
| **Total de Módulos** | 14 (incluyendo módulo 0) |
| **Total de Clases** | 37 |
| **Duración Estimada** | ~3-4 horas |
| **Nivel** | Básico |

---

## 🎯 Requisitos

**Opción A: WSL (Windows)**
- Windows 10 o superior
- WSL instalado (ver Módulo 0)

**Opción B: VM en AWS**
- Cuenta de AWS (Free Tier funciona)
- Ver Módulo 0 para crear tu VM

---

## 🗂️ Estructura de Carpetas

```
modulos/
├── 00-setup-entorno/      # Opcional - Preparar tu entorno
├── 01-navegacion-basica/  # Lo básico de la terminal
├── 02-procesamiento-texto/# grep, awk, sed
├── 03-variables-alias/    # Variables y atajos
├── 04-usuarios-permisos/  # chmod, chown, usuarios
├── 05-bash-scripting/     # Crear scripts
├── 06-cron/               # Automatización
├── 07-ssh/                # Conexiones remotas
├── 08-diagnostico-red/    # curl, ping, dig
├── 09-gestion-paquetes/   # apt, yum
├── 10-compresion/         # tar, gzip
├── 11-json-yaml/          # jq, yq (avanzado)
├── 12-gestion-procesos/   # top, htop, kill (avanzado)
└── 13-gestion-servicios/  # nginx (avanzado)
```

---

## 🎬 ¿Cómo usar este material?

1. **Ve los videos** en orden
2. **Lee el material** de cada clase
3. **Practica** los comandos en tu terminal
4. **Haz las tareas** al final de cada clase

---

## 💡 Tips

- Usa una terminal con **tema oscuro** y **fuente grande**
- **Practica** cada comando, no solo leas
- Si algo no funciona, revisa los **errores** con calma
- **Google** es tu amigo para mensajes de error

---

## 🔗 Recursos Adicionales

- `man comando` - Manual de cualquier comando
- [tldr.sh](https://tldr.sh) - Ejemplos rápidos de comandos
- [explainshell.com](https://explainshell.com) - Explica comandos visualmente

---

*Curso creado por Jean Reyes (@jemjaf)*

# 01 Navegacion y archivos

## pwd (print working directory)

Print the directory we are currently in 

## ls (list)

```
ls
```
List the items in a directory


```
ls -l
```
Displays the list in long format, detailing file size, modification date, and permissions.


```
ls -a
```
Shows all files, including hidden files.

```
ls -la
```
Combina ambos para ver absolutamente todo con todo el detalle

## cd (change directory)

change the directory to the selected

## whoami

show the actual user

## ~

navigate to the root home user

Important !

Tambien se puede navegar al home directamente con cd sin el ~

otro caso de uso es listar archivos cuando estamos en otra ruta, ejemplo
desde cualquier ruta , deberia listar los archivos que estan en /home/anton/git

```
ls -la ~/git
```

## cat (Concatenate)
Las 3 formas más comunes de usarlo
- Ver el contenido de un archivo:
```cat notas.txt```

- Crear un archivo rápido desde cero:
```cat > nuevo_archivo.txt```

- Unir (concatenar) dos archivos en uno nuevo:
```cat archivo1.txt archivo2.txt > archivo_final.txt```

## head

por default muestra las primeras 10 lineas de un archivo. 

ejemplo
```
head ejemplo.txt
```

se puede agregar el flag -n para especificar el numero de lineas a mostrar
```
head -n 2 ejemplo.txt
```

## tail
por default muestra las ultimas 10 lineas de un archivo. 

ejemplo
```
tail ejemplo.txt
```

se puede agregar el flag -n para especificar el numero de lineas a mostrar
```
tail -n 2 ejemplo.txt
```

otro ejemplo es 


```
tail -f ejemplo.txt
```

## less
